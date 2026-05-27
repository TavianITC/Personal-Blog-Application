using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Personal_Blog_Application.Services.Profile
{
    public class ProfileService : IProfileService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _env;

        private const long MaxAvatarBytes = 2 * 1024 * 1024;

        // 200×200 @ Q90 lands at ~25–40 KB for typical photographs — well inside
        // the 50 KB ceiling, and visibly crisper than Q80. Drop back toward 80
        // if a harder cap is needed.
        private const int AvatarPixels = 200;
        private const int JpegQuality = 90;

        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif" };
        private static readonly HashSet<string> AllowedContentTypes =
            new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif" };

        public ProfileService(
            AppDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public async Task<OperationResult<ProfileViewModel>> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return OperationResult<ProfileViewModel>.NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var blogCount = await _context.Blogs.CountAsync(b => b.CreatedBy == user.Id);
            var commentCount = await _context.Comments.CountAsync(c => c.CreatedBy == user.Id);

            return OperationResult<ProfileViewModel>.Ok(new ProfileViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "USER",
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                BlogCount = blogCount,
                CommentCount = commentCount
            });
        }

        public async Task<OperationResult> UpdateAvatarAsync(string userId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return OperationResult.Fail("Please choose an image to upload.");

            if (file.Length > MaxAvatarBytes)
                return OperationResult.Fail("Image must be 2 MB or smaller.");

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(file.ContentType))
                return OperationResult.Fail("Only JPG, PNG, or GIF images are allowed.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return OperationResult.NotFound();

            var avatarsDir = Path.Combine(_env.WebRootPath, "avatars");
            Directory.CreateDirectory(avatarsDir);

            // Re-encode as JPEG regardless of input format — predictable size cap
            // and avoids the alpha / animation edge cases of PNG/GIF. GUID filename
            // keeps user-supplied names out of the filesystem path (defence against
            // traversal / collisions).
            var newName = $"{Guid.NewGuid():N}.jpg";
            var newPath = Path.Combine(avatarsDir, newName);

            try
            {
                await using var input = file.OpenReadStream();
                using var image = await Image.LoadAsync(input);

                // ResizeMode.Crop: scale uniformly so the image covers a 200×200
                // box, then center-crop the longer side. Preserves aspect ratio
                // (no stretching) and guarantees square output.
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(AvatarPixels, AvatarPixels),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

                await image.SaveAsJpegAsync(newPath, new JpegEncoder { Quality = JpegQuality });
            }
            catch (Exception ex) when (ex is UnknownImageFormatException || ex is InvalidImageContentException)
            {
                return OperationResult.Fail("That file doesn't look like a valid image.");
            }

            var oldUrl = user.AvatarUrl;
            user.AvatarUrl = $"/avatars/{newName}";

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                // Roll back the just-written file so it doesn't orphan in wwwroot.
                try { File.Delete(newPath); } catch { /* ignored */ }
                return OperationResult.Fail(
                    "Could not update avatar: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            // Best-effort cleanup of the previous file so wwwroot/avatars doesn't
            // accumulate orphans. Swallow failures — they shouldn't block the update.
            if (!string.IsNullOrEmpty(oldUrl) && oldUrl.StartsWith("/avatars/", StringComparison.Ordinal))
            {
                var oldPath = Path.Combine(
                    _env.WebRootPath,
                    oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(oldPath))
                {
                    try { File.Delete(oldPath); } catch { /* ignored */ }
                }
            }

            return OperationResult.Ok();
        }
    }
}
