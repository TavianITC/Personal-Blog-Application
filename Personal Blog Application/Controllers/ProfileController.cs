using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Services.Profile;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profile;
        private readonly UserManager<User> _userManager;

        public ProfileController(IProfileService profile, UserManager<User> userManager)
        {
            _profile = profile;
            _userManager = userManager;
        }

        // GET /profile
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _profile.GetProfileAsync(userId);

            if (result.Status == ResultStatus.NotFound) return NotFound();
            return View(result.Value);
        }

        // POST /profile/avatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Avatar(AvatarUploadViewModel model)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _profile.UpdateAvatarAsync(userId, model.File);

            switch (result.Status)
            {
                case ResultStatus.NotFound:
                    return NotFound();
                case ResultStatus.Success:
                    TempData["Success"] = "Avatar updated.";
                    break;
                default:
                    TempData["Error"] = result.Errors.FirstOrDefault() ?? "Could not update avatar.";
                    break;
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /profile/change-password
        [HttpPost]
        [ActionName("ChangePassword")]
        [Route("/profile/change-password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            // Failures stay inside the password modal (TempData["PasswordError"]
            // is read by Profile/Index.cshtml to re-open the modal + show the
            // banner). The global TempData["Error"] is reserved for page-level
            // errors so it isn't used here.
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();
                TempData["PasswordError"] = firstError ?? "Something went wrong!";
                return RedirectToAction(nameof(Index));
            }

            var userId = _userManager.GetUserId(User)!;
            var result = await _profile.ChangePasswordAsync(
                userId, model.CurrentPassword, model.NewPassword);

            switch (result.Status)
            {
                case ResultStatus.NotFound:
                    return NotFound();
                case ResultStatus.Success:
                    TempData["Success"] = "Password changed.";
                    break;
                default:
                    TempData["PasswordError"] = result.Errors.FirstOrDefault() ?? "Could not change password.";
                    break;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
