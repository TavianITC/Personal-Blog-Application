using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Profile
{
    public interface IProfileService
    {
        Task<OperationResult<ProfileViewModel>> GetProfileAsync(string userId);

        // Validates + re-encodes the uploaded image and writes it to wwwroot/avatars.
        // Returns Ok on success; ValidationError for size/format issues; NotFound
        // if the caller's user record can't be loaded.
        Task<OperationResult> UpdateAvatarAsync(string userId, IFormFile? file);
    }
}
