using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Services.Auth
{
    public interface IAuthService
    {
        Task<OperationResult> LoginAsync(LoginViewModel model);

        Task<OperationResult> RegisterAsync(RegisterViewModel model);

        Task LogoutAsync();
    }
}
