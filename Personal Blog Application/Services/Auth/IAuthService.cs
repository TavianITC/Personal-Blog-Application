using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Auth
{
    public interface IAuthService
    {
        // Returns Success on sign-in; otherwise ValidationError with either
        // FieldErrors (e.g. Email/Username clash on Register) or a generic
        // summary Errors list (e.g. "Invalid email or password.").
        Task<OperationResult> LoginAsync(LoginViewModel model);

        Task<OperationResult> RegisterAsync(RegisterViewModel model);

        Task LogoutAsync();
    }
}
