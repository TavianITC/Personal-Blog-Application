using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Blogs
{
    public class BlogService : IBlogService
    {
        private readonly AppDbContext _context;

        public BlogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Blog>> GetFeedAsync(
            string? title, string? author, string? sort, string userId, bool isAdmin)
        {
            IQueryable<Blog> query = _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments)
                .AsSplitQuery();

            if (!isAdmin)
                query = query.Where(b => b.Status == "PUBLISHED");

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));

            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.User.UserName!.Contains(author));

            query = ApplySort(query, sort);
            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<Blog>> GetMineAsync(
            string? title, string? author, string? sort, string userId)
        {
            IQueryable<Blog> query = _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments)
                .AsSplitQuery()
                .Where(b => b.CreatedBy == userId);

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));

            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.User.UserName!.Contains(author));

            query = ApplySort(query, sort);
            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<Blog>> GetHomeFeedAsync(string userId, int take = 20)
        {
            return await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments)
                .AsSplitQuery()
                .Where(b => b.Status == "PUBLISHED" && b.CreatedBy != userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<OperationResult<BlogDetailViewModel>> GetDetailAsync(
            int id, string userId, bool isAdmin, string? from)
        {
            var blog = await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments).ThenInclude(c => c.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (blog == null)
                return OperationResult<BlogDetailViewModel>.NotFound();

            if (blog.Status != "PUBLISHED" && !CanModify(blog, userId, isAdmin))
                return OperationResult<BlogDetailViewModel>.Forbidden();

            var vm = new BlogDetailViewModel
            {
                Blog = blog,
                Comments = blog.Comments.OrderByDescending(c => c.CreatedAt).ToList(),
                NewComment = new CommentCreateViewModel
                {
                    BlogId = id,
                    From = string.IsNullOrEmpty(from) ? null : from
                }
            };
            return OperationResult<BlogDetailViewModel>.Ok(vm);
        }

        public async Task<OperationResult<BlogCreateViewModel>> GetForEditAsync(
            int id, string userId, bool isAdmin)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
                return OperationResult<BlogCreateViewModel>.NotFound();
            if (!CanModify(blog, userId, isAdmin))
                return OperationResult<BlogCreateViewModel>.Forbidden();

            return OperationResult<BlogCreateViewModel>.Ok(new BlogCreateViewModel
            {
                Title = blog.Title,
                Content = blog.Content,
                Priority = blog.Priority,
                Status = blog.Status
            });
        }

        public async Task<OperationResult<CreateBlogOutcome>> CreateAsync(
            BlogCreateViewModel model, string userId)
        {
            var blog = new Blog
            {
                Title = model.Title,
                Content = model.Content,
                Priority = model.Priority,
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            return OperationResult<CreateBlogOutcome>.Ok(
                new CreateBlogOutcome(blog.Id, blog.Status));
        }

        public async Task<OperationResult> UpdateAsync(
            int id, BlogCreateViewModel model, string userId, bool isAdmin)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return OperationResult.NotFound();
            if (!CanModify(blog, userId, isAdmin)) return OperationResult.Forbidden();

            blog.Title = model.Title;
            blog.Content = model.Content;
            blog.Priority = model.Priority;
            blog.Status = model.Status;
            blog.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> DeleteAsync(int id, string userId, bool isAdmin)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return OperationResult.NotFound();
            if (!CanModify(blog, userId, isAdmin)) return OperationResult.Forbidden();

            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        private static IQueryable<Blog> ApplySort(IQueryable<Blog> query, string? sort) => sort switch
        {
            "priority_asc" => query.OrderBy(b => b.Priority).ThenByDescending(b => b.CreatedAt),
            "priority_desc" or "priority" =>
                query.OrderByDescending(b => b.Priority).ThenByDescending(b => b.CreatedAt),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        private static bool CanModify(Blog blog, string userId, bool isAdmin) =>
            isAdmin || blog.CreatedBy == userId;
    }
}
