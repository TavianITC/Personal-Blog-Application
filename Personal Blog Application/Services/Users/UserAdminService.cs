using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Users
{
    public class UserAdminService : IUserAdminService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public UserAdminService(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IReadOnlyList<UserListItemViewModel>> ListAsync(string? q)
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
                    BlogCount = blogCounts.GetValueOrDefault(u.Id)
                });
            }
            return items;
        }

        public async Task<OperationResult<UserEditViewModel>> GetForEditAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return OperationResult<UserEditViewModel>.NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return OperationResult<UserEditViewModel>.NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return OperationResult<UserEditViewModel>.Ok(new UserEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "USER",
                IsActive = user.IsActive
            });
        }

        public async Task<OperationResult<UserUpdateOutcome>> UpdateAsync(
            string id, UserEditViewModel model, string currentUserId)
        {
            if (string.IsNullOrEmpty(id) || id != model.Id)
                return OperationResult<UserUpdateOutcome>.Fail("Invalid user id.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return OperationResult<UserUpdateOutcome>.NotFound();

            var isSelf = user.Id == currentUserId;
            var fieldErrors = new Dictionary<string, string>();

            // Guard against an admin locking themselves out by demotion / deactivation.
            if (isSelf)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.FirstOrDefault() != model.Role)
                    fieldErrors[nameof(model.Role)] = "You cannot change your own role.";
                if (!model.IsActive)
                    fieldErrors[nameof(model.IsActive)] = "You cannot deactivate your own account.";
            }

            // Up-front username clash check to surface a friendly error before
            // Identity's lower-level "Username already taken" can fire.
            var trimmedName = model.UserName?.Trim() ?? string.Empty;
            var renamed = !string.Equals(user.UserName, trimmedName, StringComparison.Ordinal);
            if (renamed)
            {
                var clash = await _userManager.FindByNameAsync(trimmedName);
                if (clash != null && clash.Id != user.Id)
                    fieldErrors[nameof(model.UserName)] = "This username is already taken.";
            }

            if (fieldErrors.Count > 0)
                return new OperationResult<UserUpdateOutcome>(
                    ResultStatus.ValidationError, default, Array.Empty<string>(), fieldErrors);

            // SetUserNameAsync rewrites the normalized name and persists in one go,
            // including IsActive (already mutated on the tracked entity).
            user.IsActive = model.IsActive;
            IdentityResult saveResult = renamed
                ? await _userManager.SetUserNameAsync(user, trimmedName)
                : await _userManager.UpdateAsync(user);

            if (!saveResult.Succeeded)
                return OperationResult<UserUpdateOutcome>.Fail(
                    saveResult.Errors.Select(e => e.Description).ToArray());

            // Role swap (Remove old, Add new). Only triggered when actually changing.
            var oldRoles = await _userManager.GetRolesAsync(user);
            if (!oldRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            return OperationResult<UserUpdateOutcome>.Ok(
                new UserUpdateOutcome(user.UserName ?? string.Empty, isSelf && renamed));
        }

        public async Task<OperationResult<string>> DeleteAsync(string id, string currentUserId)
        {
            if (string.IsNullOrEmpty(id))
                return OperationResult<string>.NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return OperationResult<string>.NotFound();

            if (user.Id == currentUserId)
                return new OperationResult<string>(
                    ResultStatus.Conflict, null,
                    new[] { "You cannot delete your own account." });

            var deletedName = user.UserName ?? string.Empty;

            // User → Comments uses NoAction (see AppDbContext.OnModelCreating) to avoid
            // multiple-cascade-path errors on SQL Server. Clear the user's comments by
            // hand before delete — cascade then handles Blogs (and comments on those
            // blogs) automatically.
            var userComments = _context.Comments.Where(c => c.CreatedBy == user.Id);
            _context.Comments.RemoveRange(userComments);
            await _context.SaveChangesAsync();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return OperationResult<string>.Fail(
                    "Failed to delete user: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            return OperationResult<string>.Ok(deletedName);
        }
    }
}
