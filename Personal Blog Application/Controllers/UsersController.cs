using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Services.Users;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UsersController : Controller
    {
        private readonly IUserAdminService _users;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public UsersController(
            IUserAdminService users,
            UserManager<User> userManager,
            SignInManager<User> signInManager)
        {
            _users = users;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET /users?q=
        public async Task<IActionResult> Index(string? q)
        {
            var items = await _users.ListAsync(q);
            ViewBag.Query = q;
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            return View(items);
        }

        // GET /users/edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            var result = await _users.GetForEditAsync(id);
            if (result.Status == ResultStatus.NotFound) return NotFound();

            ViewBag.IsSelf = result.Value!.Id == _userManager.GetUserId(User);
            return View(result.Value);
        }

        // POST /users/edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            ViewBag.IsSelf = model.Id == currentUserId;

            if (!ModelState.IsValid)
            {
                // Email + AvatarUrl are display-only in the form, re-hydrate from store before re-render.
                var existing = await _userManager.FindByIdAsync(id);
                if (existing != null)
                {
                    model.Email = existing.Email ?? string.Empty;
                    model.AvatarUrl = existing.AvatarUrl;
                }
                return View(model);
            }

            var result = await _users.UpdateAsync(id, model, currentUserId);

            if (result.Status == ResultStatus.NotFound) return NotFound();
            if (result.Status == ResultStatus.ValidationError)
            {
                if (result.FieldErrors != null)
                {
                    foreach (var (field, message) in result.FieldErrors)
                        ModelState.AddModelError(field, message);
                }
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err);

                var existing = await _userManager.FindByIdAsync(id);
                if (existing != null)
                {
                    model.Email = existing.Email ?? string.Empty;
                    model.AvatarUrl = existing.AvatarUrl;
                }
                return View(model);
            }

            // Refresh the auth cookie so navbar / User.Identity.Name pick up the new
            // username without forcing the admin to sign out and back in.
            if (result.Value!.NeedsSignInRefresh)
            {
                var refreshed = await _userManager.FindByIdAsync(id);
                if (refreshed != null)
                    await _signInManager.RefreshSignInAsync(refreshed);
            }

            TempData["Success"] = $"User '{result.Value.UserName}' updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST /users/delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var result = await _users.DeleteAsync(id, currentUserId);

            switch (result.Status)
            {
                case ResultStatus.NotFound:
                    return NotFound();
                case ResultStatus.Conflict:
                    TempData["Error"] = result.Errors.FirstOrDefault();
                    return RedirectToAction(nameof(Index));
                case ResultStatus.ValidationError:
                    TempData["Error"] = string.Join("; ", result.Errors);
                    return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"User '{result.Value}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
