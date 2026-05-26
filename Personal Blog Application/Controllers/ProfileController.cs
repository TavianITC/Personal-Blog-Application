using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _env;

        private const long MaxAvatarBytes = 2 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif" };
        private static readonly HashSet<string> AllowedContentTypes =
            new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif" };

        public ProfileController(AppDbContext context, UserManager<User> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // GET /profile
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var blogCount = await _context.Blogs.CountAsync(b => b.CreatedBy == user.Id);
            var commentCount = await _context.Comments.CountAsync(c => c.CreatedBy == user.Id);

            var vm = new ProfileViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "USER",
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                BlogCount = blogCount,
                CommentCount = commentCount
            };
            return View(vm);
        }

        // POST /profile/avatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Avatar(AvatarUploadViewModel model)
        {
            var file = model.File;

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (file.Length > MaxAvatarBytes)
            {
                TempData["Error"] = "Image must be 2 MB or smaller.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(file.ContentType))
            {
                TempData["Error"] = "Only JPG, PNG, or GIF images are allowed.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var avatarsDir = Path.Combine(_env.WebRootPath, "avatars");
            Directory.CreateDirectory(avatarsDir);

            // GUID filename keeps user-supplied names out of the filesystem path
            // (defence against path traversal / collisions).
            var newName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var newPath = Path.Combine(avatarsDir, newName);

            await using (var stream = System.IO.File.Create(newPath))
            {
                await file.CopyToAsync(stream);
            }

            var oldUrl = user.AvatarUrl;
            user.AvatarUrl = $"/avatars/{newName}";

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                System.IO.File.Delete(newPath);
                TempData["Error"] = "Could not update avatar: " +
                    string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // Best-effort cleanup of the previous file so wwwroot/avatars doesn't
            // accumulate orphans. Swallow failures — they shouldn't block the update.
            if (!string.IsNullOrEmpty(oldUrl) && oldUrl.StartsWith("/avatars/", StringComparison.Ordinal))
            {
                var oldPath = Path.Combine(_env.WebRootPath, oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath))
                {
                    try { System.IO.File.Delete(oldPath); } catch { /* ignored */ }
                }
            }

            TempData["Success"] = "Avatar updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
