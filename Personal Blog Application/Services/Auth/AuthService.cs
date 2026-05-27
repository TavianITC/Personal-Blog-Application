using Microsoft.AspNetCore.Identity;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AuthService(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<OperationResult> LoginAsync(LoginViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return OperationResult.Fail("Invalid email or password.");

            if (!user.IsActive)
                return OperationResult.Fail("Your account has been deactived");

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
                return OperationResult.Fail("Invalid email or password.");

            return OperationResult.Ok();
        }

        public async Task<OperationResult> RegisterAsync(RegisterViewModel model)
        {
            // Field-level duplicate checks first so the form highlights the right input.
            var fieldErrors = new Dictionary<string, string>();
            if (await _userManager.FindByEmailAsync(model.Email) != null)
                fieldErrors[nameof(model.Email)] = "Email already exists.";
            if (await _userManager.FindByNameAsync(model.Username) != null)
                fieldErrors[nameof(model.Username)] = "Username already exists.";

            if (fieldErrors.Count > 0)
                return OperationResult.FailFields(fieldErrors);

            var user = new User
            {
                UserName = model.Username,
                Email = model.Email,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
                return OperationResult.Fail(
                    createResult.Errors.Select(e => e.Description).ToArray());

            // Newly-registered users always get the "USER" role.
            await _userManager.AddToRoleAsync(user, "USER");
            return OperationResult.Ok();
        }

        public Task LogoutAsync() => _signInManager.SignOutAsync();
    }
}
