using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Users
{
    public interface IUserAdminService
    {
        Task<IReadOnlyList<UserListItemViewModel>> ListAsync(string? q);

        Task<OperationResult<UserEditViewModel>> GetForEditAsync(string id);

        Task<OperationResult<UserUpdateOutcome>> UpdateAsync(
            string id, UserEditViewModel model, string currentUserId);

        Task<OperationResult<string>> DeleteAsync(string id, string currentUserId);
    }

    // NeedsSignInRefresh: true when the admin renamed themselves — the controller
    // must call SignInManager.RefreshSignInAsync to update the cookie's UserName claim.
    public record UserUpdateOutcome(string UserName, bool NeedsSignInRefresh);
}
