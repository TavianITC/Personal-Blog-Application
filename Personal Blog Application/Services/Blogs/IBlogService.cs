using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;
using X.PagedList;

namespace Personal_Blog_Application.Services.Blogs
{
    public interface IBlogService
    {
        Task<IPagedList<Blog>> GetFeedAsync(
            string? title, string? author, string? sort,
            string userId, bool isAdmin,
            int page = 1, int pageSize = PaginationDefaults.PageSize);

        Task<IPagedList<Blog>> GetMineAsync(
            string? title, string? sort, string? status,
            string userId,
            int page = 1, int pageSize = PaginationDefaults.PageSize);

        // Counts per Status for the Mine tabs (PUBLISHED/PRIVATE/DRAFT).
        Task<IDictionary<string, int>> GetMyStatusCountsAsync(string userId);

        Task<IReadOnlyList<Blog>> GetHomeFeedAsync(string userId, int take = 20);

        Task<OperationResult<BlogDetailViewModel>> GetDetailAsync(
            int id, string userId, bool isAdmin, string? from,
            int commentPage = 1, int commentPageSize = PaginationDefaults.PageSize);

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
