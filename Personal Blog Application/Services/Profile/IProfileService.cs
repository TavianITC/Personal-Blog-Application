using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Profile
{
    public interface IProfileService
    {
        Task<OperationResult<ProfileViewModel>> GetProfileAsync(string userId);

        Task<OperationResult> UpdateAvatarAsync(string userId, IFormFile? file);

        Task<OperationResult> ChangePasswordAsync(
            string userId, string currentPassword, string newPassword);
    }
}
