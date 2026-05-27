using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Comments
{
    public interface ICommentService
    {
        // On success returns the saved Comment with User loaded plus an up-to-date
        // count of comments for the blog (so AJAX callers can refresh the badge).
        Task<OperationResult<CreatedComment>> CreateAsync(
            CommentCreateViewModel model, string userId, bool isAdmin);

        // On success returns the affected BlogId + remaining comment count.
        Task<OperationResult<DeletedComment>> DeleteAsync(int id, string userId, bool isAdmin);

        // Re-builds the Blog detail VM used when a server-rendered comment POST
        // fails validation and we want to re-render Detail with ModelState intact.
        Task<BlogDetailViewModel?> BuildDetailViewModelAsync(int blogId);
    }

    public record CreatedComment(Comment Comment, int CommentCount);
    public record DeletedComment(int BlogId, int CommentCount);
}
