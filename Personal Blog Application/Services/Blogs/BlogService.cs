using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;
using X.PagedList;
using X.PagedList.EF;

namespace Personal_Blog_Application.Services.Blogs
{
    public class BlogService : IBlogService
    {
        private readonly AppDbContext _context;

        public BlogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IPagedList<Blog>> GetFeedAsync(
            string? title, string? author, string? sort,
            string userId, bool isAdmin,
            int page = 1, int pageSize = IBlogService.DefaultPageSize)
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
            return await query.ToPagedListAsync(NormalizePage(page), pageSize);
        }

        public async Task<IPagedList<Blog>> GetMineAsync(
            string? title, string? sort, string? status,
            string userId,
            int page = 1, int pageSize = IBlogService.DefaultPageSize)
        {
            IQueryable<Blog> query = _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments)
                .AsSplitQuery()
                .Where(b => b.CreatedBy == userId);

            if (IsValidStatus(status))
                query = query.Where(b => b.Status == status);

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));

            query = ApplySort(query, sort);
            return await query.ToPagedListAsync(NormalizePage(page), pageSize);
        }

        public async Task<IDictionary<string, int>> GetMyStatusCountsAsync(string userId)
        {
            var counts = await _context.Blogs
                .Where(b => b.CreatedBy == userId)
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            // Ensure all three keys are present so the view doesn't need null checks.
            foreach (var key in new[] { "PUBLISHED", "PRIVATE", "DRAFT" })
                counts.TryAdd(key, 0);
            return counts;
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
            int id, string userId, bool isAdmin, string? from,
            int commentPage = 1, int commentPageSize = IBlogService.DefaultCommentPageSize)
        {
            var blog = await _context.Blogs
                .Include(b => b.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (blog == null)
                return OperationResult<BlogDetailViewModel>.NotFound();

            if (blog.Status != "PUBLISHED" && !CanModify(blog, userId, isAdmin))
                return OperationResult<BlogDetailViewModel>.Forbidden();

            // Paginate comments at the DB so we don't materialize the entire history
            // when a popular post has many comments.
            var pagedComments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.BlogId == id)
                .OrderByDescending(c => c.CreatedAt)
                .ToPagedListAsync(NormalizePage(commentPage), commentPageSize);

            var vm = new BlogDetailViewModel
            {
                Blog = blog,
                Comments = pagedComments,
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

        private static bool IsValidStatus(string? status) =>
            status is "DRAFT" or "PUBLISHED" or "PRIVATE";

        // X.PagedList throws on page <= 0; clamp here so callers can pass query
        // params straight through without defensive checks.
        private static int NormalizePage(int page) => page < 1 ? 1 : page;
    }
}
