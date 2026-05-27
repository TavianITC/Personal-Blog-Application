using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Blogs
{
    public interface IBlogService
    {
        Task<IReadOnlyList<Blog>> GetFeedAsync(
            string? title, string? author, string? sort, string userId, bool isAdmin);

        Task<IReadOnlyList<Blog>> GetMineAsync(
            string? title, string? author, string? sort, string userId);

        Task<IReadOnlyList<Blog>> GetHomeFeedAsync(string userId, int take = 20);

        Task<OperationResult<BlogDetailViewModel>> GetDetailAsync(
            int id, string userId, bool isAdmin, string? from);

        Task<OperationResult<BlogCreateViewModel>> GetForEditAsync(
            int id, string userId, bool isAdmin);

        Task<OperationResult<CreateBlogOutcome>> CreateAsync(
            BlogCreateViewModel model, string userId);

        Task<OperationResult> UpdateAsync(
            int id, BlogCreateViewModel model, string userId, bool isAdmin);

        Task<OperationResult> DeleteAsync(
            int id, string userId, bool isAdmin);
    }

    public record CreateBlogOutcome(int Id, string Status);
}
