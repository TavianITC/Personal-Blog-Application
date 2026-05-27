using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;
using X.PagedList;
using X.PagedList.EF;

namespace Personal_Blog_Application.Services.Comments
{
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;

        public CommentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<OperationResult<CreatedComment>> CreateAsync(
            CommentCreateViewModel model, string userId, bool isAdmin)
        {
            var blog = await _context.Blogs.FindAsync(model.BlogId);
            if (blog == null)
                return OperationResult<CreatedComment>.NotFound();

            if (blog.Status != "PUBLISHED" && !CanModerate(blog, userId, isAdmin))
                return OperationResult<CreatedComment>.Forbidden();

            var content = (model.Content ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(content))
                return OperationResult<CreatedComment>.FailField(
                    nameof(CommentCreateViewModel.Content), "Comment cannot be empty.");

            var comment = new Comment
            {
                BlogId = blog.Id,
                Content = content,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Re-fetch with User nav so callers can render the author block.
            var saved = await _context.Comments
                .Include(c => c.User)
                .FirstAsync(c => c.Id == comment.Id);

            var count = await _context.Comments.CountAsync(c => c.BlogId == blog.Id);
            return OperationResult<CreatedComment>.Ok(new CreatedComment(saved, count));
        }

        public async Task<OperationResult<DeletedComment>> DeleteAsync(
            int id, string userId, bool isAdmin)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return OperationResult<DeletedComment>.NotFound();
            if (!CanDelete(comment, userId, isAdmin))
                return OperationResult<DeletedComment>.Forbidden();

            var blogId = comment.BlogId;
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            var count = await _context.Comments.CountAsync(c => c.BlogId == blogId);
            return OperationResult<DeletedComment>.Ok(new DeletedComment(blogId, count));
        }

        public async Task<BlogDetailViewModel?> BuildDetailViewModelAsync(int blogId)
        {
            var blog = await _context.Blogs
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == blogId);

            if (blog == null) return null;

            // Validation re-render always shows page 1 — the user just submitted a
            // comment and is looking at the form, not paging through history.
            var pagedComments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.BlogId == blogId)
                .OrderByDescending(c => c.CreatedAt)
                .ToPagedListAsync(1, PaginationDefaults.PageSize);

            return new BlogDetailViewModel
            {
                Blog = blog,
                Comments = pagedComments,
                NewComment = new CommentCreateViewModel { BlogId = blogId }
            };
        }

        private static bool CanModerate(Blog blog, string userId, bool isAdmin) =>
            isAdmin || blog.CreatedBy == userId;

        private static bool CanDelete(Comment comment, string userId, bool isAdmin) =>
            isAdmin || comment.CreatedBy == userId;
    }
}
