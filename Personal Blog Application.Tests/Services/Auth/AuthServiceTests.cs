using Microsoft.AspNetCore.Identity;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Auth;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Tests.Helpers;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Tests.Services.Auth
{
    public class AuthServiceTests
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly AuthService _sut;

        public AuthServiceTests()
        {
            _userManager = IdentityFakes.CreateUserManager<User>();
            _signInManager = IdentityFakes.CreateSignInManager<User>(_userManager);
            _sut = new AuthService(_userManager, _signInManager);
        }

        // ─── LoginAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_WhenEmailNotFound_ReturnsInvalidCredentialsError()
        {
            // Arrange
            A.CallTo(() => _userManager.FindByEmailAsync("missing@example.com"))
                .Returns(Task.FromResult<User?>(null));
            var model = new LoginViewModel { Email = "missing@example.com", Password = "any" };

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.Should().Be("Invalid email or password.");
            // SignInManager must NOT be touched when user lookup already fails.
            A.CallTo(_signInManager).Where(c => c.Method.Name == nameof(SignInManager<User>.PasswordSignInAsync))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task LoginAsync_WhenUserIsInactive_ReturnsDeactivatedError()
        {
            // Arrange
            var user = new User { Email = "user@example.com", IsActive = false };
            A.CallTo(() => _userManager.FindByEmailAsync("user@example.com")).Returns(user);
            var model = new LoginViewModel { Email = "user@example.com", Password = "any" };

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("deactived", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsWrong_ReturnsInvalidCredentialsError()
        {
            // Arrange
            var user = new User { Email = "user@example.com", IsActive = true };
            A.CallTo(() => _userManager.FindByEmailAsync("user@example.com")).Returns(user);
            A.CallTo(() => _signInManager.PasswordSignInAsync(user, "wrong", false, false))
                .Returns(SignInResult.Failed);
            var model = new LoginViewModel { Email = "user@example.com", Password = "wrong", RememberMe = false };

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().Contain("Invalid email or password.");
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsValid_ReturnsSuccess()
        {
            // Arrange
            var user = new User { Email = "user@example.com", IsActive = true };
            A.CallTo(() => _userManager.FindByEmailAsync("user@example.com")).Returns(user);
            A.CallTo(() => _signInManager.PasswordSignInAsync(user, "correct", true, false))
                .Returns(SignInResult.Success);
            var model = new LoginViewModel
            {
                Email = "user@example.com",
                Password = "correct",
                RememberMe = true
            };

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            result.Success.Should().BeTrue();
            result.Status.Should().Be(ResultStatus.Success);
        }

        // ─── RegisterAsync ──────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_WhenUserIsNew_CreatesAccountAndAssignsUserRole()
        {
            // Arrange
            A.CallTo(() => _userManager.FindByEmailAsync("new@example.com"))
                .Returns(Task.FromResult<User?>(null));
            A.CallTo(() => _userManager.FindByNameAsync("newcomer"))
                .Returns(Task.FromResult<User?>(null));
            A.CallTo(() => _userManager.CreateAsync(A<User>._, "Secret123"))
                .Returns(IdentityResult.Success);
            A.CallTo(() => _userManager.AddToRoleAsync(A<User>._, "USER"))
                .Returns(IdentityResult.Success);
            var model = new RegisterViewModel
            {
                Email = "new@example.com",
                Username = "newcomer",
                Password = "Secret123",
                ConfirmPassword = "Secret123"
            };

            // Act
            var result = await _sut.RegisterAsync(model);

            // Assert
            result.Success.Should().BeTrue();
            // Verify the newly-created user is assigned the USER role (the project
            // contract: registration never grants ADMIN).
            A.CallTo(() => _userManager.AddToRoleAsync(
                A<User>.That.Matches(u => u.Email == "new@example.com"),
                "USER"))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsEmailFieldError()
        {
            // Arrange
            A.CallTo(() => _userManager.FindByEmailAsync("dup@example.com"))
                .Returns(new User { Email = "dup@example.com" });
            A.CallTo(() => _userManager.FindByNameAsync(A<string>._))
                .Returns(Task.FromResult<User?>(null));
            var model = new RegisterViewModel
            {
                Email = "dup@example.com",
                Username = "uniqueName",
                Password = "Secret123",
                ConfirmPassword = "Secret123"
            };

            // Act
            var result = await _sut.RegisterAsync(model);

            // Assert
            result.Success.Should().BeFalse();
            result.FieldErrors.Should().NotBeNull();
            result.FieldErrors!.Should().ContainKey(nameof(RegisterViewModel.Email))
                .WhoseValue.Should().Be("Email already exists.");
            // No attempt to create when there's already a duplicate.
            A.CallTo(() => _userManager.CreateAsync(A<User>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task RegisterAsync_WhenUsernameAlreadyExists_ReturnsUsernameFieldError()
        {
            // Arrange
            A.CallTo(() => _userManager.FindByEmailAsync(A<string>._))
                .Returns(Task.FromResult<User?>(null));
            A.CallTo(() => _userManager.FindByNameAsync("taken"))
                .Returns(new User { UserName = "taken" });
            var model = new RegisterViewModel
            {
                Email = "fresh@example.com",
                Username = "taken",
                Password = "Secret123",
                ConfirmPassword = "Secret123"
            };

            // Act
            var result = await _sut.RegisterAsync(model);

            // Assert
            result.Success.Should().BeFalse();
            result.FieldErrors.Should().NotBeNull();
            result.FieldErrors!.Should().ContainKey(nameof(RegisterViewModel.Username));
            A.CallTo(() => _userManager.AddToRoleAsync(A<User>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
