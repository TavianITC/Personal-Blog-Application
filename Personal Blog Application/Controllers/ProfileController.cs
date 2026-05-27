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
    }
}
