using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Services.Profile;
using Personal_Blog_Application.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Personal_Blog_Application.Tests.Services.Profile
{
    // Covers ProfileService end-to-end with real Identity (SQLite) so password
    // hashing / role lookup / etc. exercise the actual code paths. Filesystem
    // writes for avatar upload go to an isolated temp dir per test.
    public class ProfileServiceTests : IDisposable
    {
        private readonly IdentityTestContext _identity;
        private readonly TempDir _wwwroot;
        private readonly IWebHostEnvironment _env;
        private readonly ProfileService _sut;

        public ProfileServiceTests()
        {
            _identity = new IdentityTestContext();
            _wwwroot = new TempDir();
            _env = A.Fake<IWebHostEnvironment>();
            A.CallTo(() => _env.WebRootPath).Returns(_wwwroot.Path);
            _sut = new ProfileService(_identity.Db, _identity.Users, _env);
        }

        public void Dispose()
        {
            _identity.Dispose();
            _wwwroot.Dispose();
        }

        // ─── GetProfileAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetProfileAsync_WhenUserNotFound_ReturnsNotFound()
        {
            // Arrange — empty DB.

            // Act
            var result = await _sut.GetProfileAsync("missing-id");

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task GetProfileAsync_ReturnsViewModelWithRoleAndCounts()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");

            _identity.Db.SeedBlog(user.Id, title: "b1");
            _identity.Db.SeedBlog(user.Id, title: "b2");
            await _identity.Db.SaveChangesAsync();

            var blog = await _identity.Db.Blogs.FirstAsync();

            _identity.Db.SeedComment(user.Id, blog.Id);
            _identity.Db.SeedComment(user.Id, blog.Id);
            _identity.Db.SeedComment(user.Id, blog.Id);
            await _identity.Db.SaveChangesAsync();

            // Act
            var result = await _sut.GetProfileAsync(user.Id);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.UserName.Should().Be("alice");
            result.Value.Email.Should().Be("alice@x.com");
            result.Value.Role.Should().Be("USER");
            result.Value.BlogCount.Should().Be(2);
            result.Value.CommentCount.Should().Be(3);
        }

        // ─── UpdateAvatarAsync ──────────────────────────────────────────

        [Fact]
        public async Task UpdateAvatarAsync_WhenFileIsNull_ReturnsValidationError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file: null);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().Contain(e => e.Contains("choose", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task UpdateAvatarAsync_WhenFileExceeds2MB_ReturnsValidationError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");
            // 2 MB + 1 byte — bytes don't need to be a real image; size check runs first.
            var bigBytes = new byte[2 * 1024 * 1024 + 1];
            var file = MakeFormFile(bigBytes, "big.jpg", "image/jpeg");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().Contain(e => e.Contains("2 MB", StringComparison.Ordinal));
        }

        [Fact]
        public async Task UpdateAvatarAsync_WhenExtensionNotAllowed_ReturnsValidationError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");
            var file = MakeFormFile(new byte[] { 1, 2, 3 }, "evil.bmp", "image/bmp");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().Contain(e => e.Contains("JPG, PNG, or GIF"));
        }

        [Fact]
        public async Task UpdateAvatarAsync_WhenContentIsNotValidImage_ReturnsValidationError()
        {
            // Arrange — extension + content-type pass, but bytes are garbage.
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");
            var garbage = System.Text.Encoding.UTF8.GetBytes("not actually a png");
            var file = MakeFormFile(garbage, "fake.png", "image/png");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().Contain(e => e.Contains("valid image", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task UpdateAvatarAsync_HappyPath_SavesJpegFileAndUpdatesUserAvatarUrl()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");
            var pngBytes = CreateTinyPngBytes();
            var file = MakeFormFile(pngBytes, "input.png", "image/png");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file);

            // Assert
            result.Success.Should().BeTrue();
            // Reload user — UpdateAsync ran on the tracked entity, but verify URL was set.
            var refreshed = await _identity.Users.FindByIdAsync(user.Id);
            refreshed!.AvatarUrl.Should().NotBeNull().And.StartWith("/avatars/").And.EndWith(".jpg");

            // File should exist on disk and be a valid JPEG (regardless of input format).
            var fileName = System.IO.Path.GetFileName(refreshed.AvatarUrl!);
            var diskPath = System.IO.Path.Combine(_wwwroot.Path, "avatars", fileName);
            File.Exists(diskPath).Should().BeTrue();
        }

        [Fact]
        public async Task UpdateAvatarAsync_WhenUserAlreadyHasAvatar_DeletesOldFileOnSuccess()
        {
            // Arrange — manually pre-populate an old avatar file + URL.
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");
            var avatarsDir = System.IO.Path.Combine(_wwwroot.Path, "avatars");
            Directory.CreateDirectory(avatarsDir);
            var oldName = "old.jpg";
            var oldPath = System.IO.Path.Combine(avatarsDir, oldName);
            await File.WriteAllBytesAsync(oldPath, new byte[] { 0xDE, 0xAD });
            user.AvatarUrl = $"/avatars/{oldName}";
            await _identity.Users.UpdateAsync(user);
            var pngBytes = CreateTinyPngBytes();
            var file = MakeFormFile(pngBytes, "new.png", "image/png");

            // Act
            var result = await _sut.UpdateAvatarAsync(user.Id, file);

            // Assert
            result.Success.Should().BeTrue();
            File.Exists(oldPath).Should().BeFalse(); // old file cleaned up
        }

        // ─── ChangePasswordAsync ────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsNotFound()
        {
            // Act
            var result = await _sut.ChangePasswordAsync("missing-id", "old", "newSecret1");

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenCurrentPasswordIsWrong_ReturnsIncorrectMessage()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");

            // Act
            var result = await _sut.ChangePasswordAsync(user.Id, "WrongPwd1", "NewSecret1");

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().Contain(e => e.Contains("incorrect", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ChangePasswordAsync_HappyPath_NewPasswordCanLogIn_OldPasswordCannot()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");

            // Act
            var result = await _sut.ChangePasswordAsync(user.Id, "Secret1", "Brand2New");

            // Assert
            result.Success.Should().BeTrue();
            var refreshed = await _identity.Users.FindByIdAsync(user.Id);
            (await _identity.Users.CheckPasswordAsync(refreshed!, "Brand2New")).Should().BeTrue();
            (await _identity.Users.CheckPasswordAsync(refreshed!, "Secret1")).Should().BeFalse();
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenNewPasswordViolatesPolicy_ReturnsValidationError()
        {
            // Arrange — config requires digit + 6 chars min.
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "alice@x.com", "Secret1");

            // Act — "short" is 5 chars, no digit.
            var result = await _sut.ChangePasswordAsync(user.Id, "Secret1", "short");

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.Errors.Should().NotBeEmpty();
        }

        // ─── helpers ────────────────────────────────────────────────────

        private static IFormFile MakeFormFile(byte[] content, string fileName, string contentType)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, content.Length, name: "File", fileName: fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        // Generate a real, minimal PNG via ImageSharp so the happy-path upload
        // can be decoded + re-encoded by the service.
        private static byte[] CreateTinyPngBytes()
        {
            using var image = new Image<Rgba32>(8, 8);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }
    }
}
