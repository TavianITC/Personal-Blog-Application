using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public UsersController(AppDbContext context, UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET /users?q=
        public async Task<IActionResult> Index(string? q)
        {
            IQueryable<User> query = _userManager.Users;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(u =>
                    (u.UserName != null && EF.Functions.Like(u.UserName, $"%{term}%")) ||
                    (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")));
            }

            var users = await query.OrderBy(u => u.UserName).ToListAsync();

            // Single grouped query for blog counts to avoid N+1.
            var userIds = users.Select(u => u.Id).ToList();
            var blogCounts = await _context.Blogs
                .Where(b => userIds.Contains(b.CreatedBy))
                .GroupBy(b => b.CreatedBy)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var items = new List<UserListItemViewModel>(users.Count);
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(new UserListItemViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "USER",
                    IsActive = u.IsActive,
                    BlogCount = blogCounts.GetValueOrDefault(u.Id),
                    AvatarUrl = u.AvatarUrl
                });
            }

            ViewBag.Query = q;
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            return View(items);
        }

        // GET /users/edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var vm = new UserEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "USER",
                IsActive = user.IsActive,
                AvatarUrl = user.AvatarUrl
            };

            ViewBag.IsSelf = user.Id == _userManager.GetUserId(User);
            return View(vm);
        }

        // POST /users/edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (string.IsNullOrEmpty(id) || id != model.Id) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var isSelf = user.Id == _userManager.GetUserId(User);
            ViewBag.IsSelf = isSelf;

            // Guard against the last admin locking themselves out.
            if (isSelf)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.FirstOrDefault() != model.Role)
                    ModelState.AddModelError(nameof(model.Role), "You cannot change your own role.");
                if (!model.IsActive)
                    ModelState.AddModelError(nameof(model.IsActive), "You cannot deactivate your own account.");
            }

            // Reject taken usernames up-front so the user sees a friendly message
            // instead of Identity's lower-level "Username already taken" error.
            var trimmedName = model.UserName?.Trim() ?? string.Empty;
            var renamed = !string.Equals(user.UserName, trimmedName, StringComparison.Ordinal);
            if (renamed)
            {
                var clash = await _userManager.FindByNameAsync(trimmedName);
                if (clash != null && clash.Id != user.Id)
                    ModelState.AddModelError(nameof(model.UserName), "This username is already taken.");
            }

            if (!ModelState.IsValid)
            {
                model.Email = user.Email ?? string.Empty;
                model.AvatarUrl = user.AvatarUrl;
                return View(model);
            }


            // Update UserName via SetUserNameAsync so the normalized name is rewritten
            // alongside the raw one. SetUserNameAsync persists the entity, picking up
            // IsActive in the same SaveChanges.
            user.IsActive = model.IsActive;
            IdentityResult saveResult;
            if (renamed)
                saveResult = await _userManager.SetUserNameAsync(user, trimmedName);
            else
                saveResult = await _userManager.UpdateAsync(user);

            if (!saveResult.Succeeded)
            {
                foreach (var err in saveResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                model.Email = user.Email ?? string.Empty;
                model.AvatarUrl = user.AvatarUrl;
                return View(model);
            }

            // Update Role
            var oldRoles = await _userManager.GetRolesAsync(user);
            if (!oldRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            // The auth cookie carries the old UserName claim — refresh it so the
            // navbar (and any User.Identity.Name reads) reflect the new name without
            // forcing the admin to sign out.
            if (isSelf && renamed)
                await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = $"User '{user.UserName}' updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST /users/delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            // User → Comments uses NoAction (see AppDbContext.OnModelCreating) to avoid
            // multiple-cascade-path errors on SQL Server. Clear the user's comments by
            // hand before delete — cascade then handles Blogs (and comments on those
            // blogs) automatically.

            // Remove User's comments first
            var userComments = _context.Comments.Where(c => c.CreatedBy == user.Id);
            _context.Comments.RemoveRange(userComments);
            await _context.SaveChangesAsync();

            // Delete user (will cascade delete their blogs and blog comments)
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Failed to delete user: " +
                    string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"User '{user.UserName}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
