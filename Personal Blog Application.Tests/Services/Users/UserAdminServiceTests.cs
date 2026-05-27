using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Services.Users;
using Personal_Blog_Application.Tests.Helpers;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Tests.Services.Users
{
    // Full coverage of UserAdminService against real Identity (SQLite) so role
    // assignments, normalized name lookups, and the comment-cascade-then-delete
    // sequence run through the actual machinery.
    public class UserAdminServiceTests : IDisposable
    {
        private readonly IdentityTestContext _identity;
        private readonly UserAdminService _sut;

        public UserAdminServiceTests()
        {
            _identity = new IdentityTestContext();
            _sut = new UserAdminService(_identity.Db, _identity.Users);
        }

        public void Dispose() => _identity.Dispose();

        // ─── ListAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ListAsync_ReturnsAllUsers_OrderedByUserName()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            await _identity.CreateUserAsync("charlie", "c@x.com", "Secret1");
            await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");
            await _identity.CreateUserAsync("bob", "b@x.com", "Secret1");

            // Act
            var page = await _sut.ListAsync(q: null);

            // Assert
            page.TotalItemCount.Should().Be(3);
            page.Select(u => u.UserName).Should().Equal("alice", "bob", "charlie");
        }

        [Fact]
        public async Task ListAsync_SearchByName_IsCaseInsensitive()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");
            await _identity.CreateUserAsync("bob", "b@x.com", "Secret1");

            // Act — note the upper-case query.
            var page = await _sut.ListAsync(q: "ALI");

            // Assert
            page.Should().ContainSingle().Which.UserName.Should().Be("alice");
        }

        [Fact]
        public async Task ListAsync_SearchByEmail_MatchesEmailFragments()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            await _identity.CreateUserAsync("alice", "alice@example.com", "Secret1");
            await _identity.CreateUserAsync("bob", "bob@other.com", "Secret1");

            // Act
            var page = await _sut.ListAsync(q: "example");

            // Assert
            page.Should().ContainSingle().Which.Email.Should().Be("alice@example.com");
        }

        [Fact]
        public async Task ListAsync_PaginatesAtDatabase()
        {
            // Arrange — 5 users.
            await _identity.SeedRolesAsync();
            foreach (var name in new[] { "u1", "u2", "u3", "u4", "u5" })
                await _identity.CreateUserAsync(name, $"{name}@x.com", "Secret1");

            // Act — page 2, size 2 → ordered u1..u5 → page 2 is u3,u4.
            var page = await _sut.ListAsync(q: null, page: 2, pageSize: 2);

            // Assert
            page.TotalItemCount.Should().Be(5);
            page.PageNumber.Should().Be(2);
            page.Select(u => u.UserName).Should().Equal("u3", "u4");
        }

        [Fact]
        public async Task ListAsync_IncludesBlogCountAndRolePerUser()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var alice = await _identity.CreateUserAsync("alice", "a@x.com", "Secret1", role: "ADMIN");
            var bob = await _identity.CreateUserAsync("bob", "b@x.com", "Secret1", role: "USER");
            _identity.Db.SeedBlog(alice.Id, title: "a1");
            _identity.Db.SeedBlog(alice.Id, title: "a2");
            _identity.Db.SeedBlog(bob.Id, title: "b1");
            await _identity.Db.SaveChangesAsync();

            // Act
            var page = await _sut.ListAsync(q: null);

            // Assert
            var aliceVm = page.Single(u => u.UserName == "alice");
            var bobVm = page.Single(u => u.UserName == "bob");
            aliceVm.Role.Should().Be("ADMIN");
            aliceVm.BlogCount.Should().Be(2);
            bobVm.Role.Should().Be("USER");
            bobVm.BlogCount.Should().Be(1);
        }

        // ─── GetForEditAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetForEditAsync_WhenIdMissing_ReturnsNotFound()
        {
            // Act
            var result = await _sut.GetForEditAsync(string.Empty);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task GetForEditAsync_WhenUserNotFound_ReturnsNotFound()
        {
            // Act
            var result = await _sut.GetForEditAsync("missing-id");

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task GetForEditAsync_ReturnsViewModelWithRoleAndIsActive()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "a@x.com", "Secret1", role: "ADMIN");

            // Act
            var result = await _sut.GetForEditAsync(user.Id);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.UserName.Should().Be("alice");
            result.Value.Role.Should().Be("ADMIN");
            result.Value.IsActive.Should().BeTrue();
        }

        // ─── UpdateAsync ────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WhenIdMismatchesModelId_ReturnsValidationError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var user = await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");
            var model = new UserEditViewModel
            {
                Id = "other-id",
                UserName = "alice",
                Email = "a@x.com",
                Role = "USER",
                IsActive = true
            };

            // Act
            var result = await _sut.UpdateAsync(user.Id, model, currentUserId: "admin");

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
        }

        [Fact]
        public async Task UpdateAsync_SelfChangingOwnRole_ReturnsFieldErrorAndKeepsRole()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var admin = await _identity.CreateUserAsync("admin", "ad@x.com", "Secret1", role: "ADMIN");
            var model = new UserEditViewModel
            {
                Id = admin.Id,
                UserName = "admin",
                Email = "ad@x.com",
                Role = "USER",       // demoting self
                IsActive = true
            };

            // Act
            var result = await _sut.UpdateAsync(admin.Id, model, currentUserId: admin.Id);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.FieldErrors.Should().ContainKey(nameof(UserEditViewModel.Role));
            var roles = await _identity.Users.GetRolesAsync(admin);
            roles.Should().Contain("ADMIN");
        }

        [Fact]
        public async Task UpdateAsync_SelfDeactivating_ReturnsFieldError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var admin = await _identity.CreateUserAsync("admin", "ad@x.com", "Secret1", role: "ADMIN");
            var model = new UserEditViewModel
            {
                Id = admin.Id,
                UserName = "admin",
                Email = "ad@x.com",
                Role = "ADMIN",
                IsActive = false     // deactivating self
            };

            // Act
            var result = await _sut.UpdateAsync(admin.Id, model, currentUserId: admin.Id);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.FieldErrors.Should().ContainKey(nameof(UserEditViewModel.IsActive));
        }

        [Fact]
        public async Task UpdateAsync_UsernameAlreadyTakenByAnotherUser_ReturnsUsernameFieldError()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");
            var bob = await _identity.CreateUserAsync("bob", "b@x.com", "Secret1");
            var model = new UserEditViewModel
            {
                Id = bob.Id,
                UserName = "alice",   // already taken
                Email = "b@x.com",
                Role = "USER",
                IsActive = true
            };

            // Act
            var result = await _sut.UpdateAsync(bob.Id, model, currentUserId: "admin");

            // Assert
            result.FieldErrors.Should().ContainKey(nameof(UserEditViewModel.UserName));
        }

        [Fact]
        public async Task UpdateAsync_HappyPath_RenamesUser_SwapsRole_AndSetsRefreshFlagWhenSelfRenamed()
        {
            // Arrange — admin renames themselves but keeps role/active.
            await _identity.SeedRolesAsync();
            var admin = await _identity.CreateUserAsync("admin", "ad@x.com", "Secret1", role: "ADMIN");
            var model = new UserEditViewModel
            {
                Id = admin.Id,
                UserName = "admin-renamed",
                Email = "ad@x.com",
                Role = "ADMIN",
                IsActive = true
            };

            // Act
            var result = await _sut.UpdateAsync(admin.Id, model, currentUserId: admin.Id);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.UserName.Should().Be("admin-renamed");
            result.Value.NeedsSignInRefresh.Should().BeTrue();  // self + renamed
        }

        [Fact]
        public async Task UpdateAsync_RoleChangeAcrossUsers_RemovesOldAndAddsNew()
        {
            // Arrange — admin changes Bob's role USER → ADMIN.
            await _identity.SeedRolesAsync();
            var bob = await _identity.CreateUserAsync("bob", "b@x.com", "Secret1", role: "USER");
            var model = new UserEditViewModel
            {
                Id = bob.Id,
                UserName = "bob",
                Email = "b@x.com",
                Role = "ADMIN",
                IsActive = true
            };

            // Act
            var result = await _sut.UpdateAsync(bob.Id, model, currentUserId: "admin");

            // Assert
            result.Success.Should().BeTrue();
            var roles = await _identity.Users.GetRolesAsync(bob);
            roles.Should().BeEquivalentTo(new[] { "ADMIN" });
        }

        // ─── DeleteAsync ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_WhenIdMissing_ReturnsNotFound()
        {
            // Act
            var result = await _sut.DeleteAsync(string.Empty, currentUserId: "admin");

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task DeleteAsync_SelfDelete_ReturnsConflict()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var admin = await _identity.CreateUserAsync("admin", "ad@x.com", "Secret1", role: "ADMIN");

            // Act
            var result = await _sut.DeleteAsync(admin.Id, currentUserId: admin.Id);

            // Assert
            result.Status.Should().Be(ResultStatus.Conflict);
            result.Errors.Should().Contain(e => e.Contains("cannot delete your own", StringComparison.OrdinalIgnoreCase));
            // Verify user still exists.
            (await _identity.Users.FindByIdAsync(admin.Id)).Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_HappyPath_RemovesUserAndCascadesBlogs_AfterClearingComments()
        {
            // Arrange — bob writes a blog, alice comments on bob's blog, alice
            // is deleted. Bob's blog stays. Alice's comments on bob's blog must
            // disappear first (User→Comments uses NoAction; manual cleanup).
            await _identity.SeedRolesAsync();
            var alice = await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");
            var bob = await _identity.CreateUserAsync("bob", "b@x.com", "Secret1");
            _identity.Db.SeedBlog(bob.Id, title: "bobs post");
            await _identity.Db.SaveChangesAsync();
            var bobBlog = await _identity.Db.Blogs.FirstAsync();
            _identity.Db.SeedComment(alice.Id, bobBlog.Id);
            _identity.Db.SeedComment(alice.Id, bobBlog.Id);
            await _identity.Db.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(alice.Id, currentUserId: "admin");

            // Assert
            result.Success.Should().BeTrue();
            (await _identity.Users.FindByIdAsync(alice.Id)).Should().BeNull();
            _identity.Db.Comments.Should().BeEmpty();           // alice's comments cleared
            (await _identity.Db.Blogs.FindAsync(bobBlog.Id)).Should().NotBeNull(); // bob's blog intact
        }

        [Fact]
        public async Task DeleteAsync_ReturnsDeletedUserNameOnSuccess()
        {
            // Arrange
            await _identity.SeedRolesAsync();
            var alice = await _identity.CreateUserAsync("alice", "a@x.com", "Secret1");

            // Act
            var result = await _sut.DeleteAsync(alice.Id, currentUserId: "admin");

            // Assert
            result.Success.Should().BeTrue();
            result.Value.Should().Be("alice");
        }
    }
}
