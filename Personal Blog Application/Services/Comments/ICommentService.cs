using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Comments
{
    public interface ICommentService
    {

        Task<OperationResult<CreatedComment>> CreateAsync(
            CommentCreateViewModel model, string userId, bool isAdmin);

        Task<OperationResult<DeletedComment>> DeleteAsync(int id, string userId, bool isAdmin);

        Task<BlogDetailViewModel?> BuildDetailViewModelAsync(int blogId);
    }

    public record CreatedComment(Comment Comment, int CommentCount);
    public record DeletedComment(int BlogId, int CommentCount);
}
