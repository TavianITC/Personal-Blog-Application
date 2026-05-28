using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Comments;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Tests.Helpers;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Tests.Services.Comments
{
    // Full coverage of CommentService against SQLite in-memory.
    public class CommentServiceTests : IDisposable
    {
        private readonly SqliteTestContext _fixture;
        private readonly AppDbContext _ctx;
        private readonly CommentService _sut;

        public CommentServiceTests()
        {
            _fixture = new SqliteTestContext();
            _ctx = _fixture.Db;
            _sut = new CommentService(_ctx);
        }

        public void Dispose() => _fixture.Dispose();

        // Seed helpers live in Helpers/Seed.cs as AppDbContext extension methods.
        // Local thin wrappers keep call sites tidy.
        private User SeedUser(string id, string userName) =>
            _ctx.SeedUser(id, userName);

        private Blog SeedBlog(string ownerId, string status = "PUBLISHED") =>
            _ctx.SeedBlog(ownerId, status: status);

        private Comment SeedComment(string userId, int blogId, string content = "x", DateTime? createdAt = null) =>
            _ctx.SeedComment(userId, blogId, content, createdAt);

        // ─── CreateAsync ────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenBlogDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            SeedUser("u1", "alice");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = 999, Content = "hi" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "u1", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task CreateAsync_WhenBlogIsDraftAndCallerIsStranger_ReturnsForbidden()
        {
            // Arrange
            SeedUser("owner", "alice");
            SeedUser("stranger", "bob");
            var blog = SeedBlog("owner", status: "DRAFT");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "hi" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "stranger", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
            _ctx.Comments.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAsync_WhenBlogIsDraftAndCallerIsOwner_AllowsComment()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", status: "DRAFT");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "my own draft" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "owner", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.Comment.Content.Should().Be("my own draft");
        }

        [Fact]
        public async Task CreateAsync_WhenBlogIsDraftAndCallerIsAdmin_AllowsComment()
        {
            // Arrange
            SeedUser("owner", "alice");
            SeedUser("admin", "admin");
            var blog = SeedBlog("owner", status: "PRIVATE");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "moderating" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "admin", isAdmin: true);

            // Assert
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task CreateAsync_WhenBlogIsPublished_AllowsAnyAuthenticatedUser_AndReturnsUpdatedCount()
        {
            // Arrange
            SeedUser("owner", "alice");
            SeedUser("commenter", "bob");
            var blog = SeedBlog("owner", status: "PUBLISHED");
            await _ctx.SaveChangesAsync();
            // Two existing comments → after this create the count should be 3.
            SeedComment("owner", blog.Id);
            SeedComment("owner", blog.Id);
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "hello" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "commenter", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.CommentCount.Should().Be(3);
            result.Value.Comment.CreatedBy.Should().Be("commenter");
        }

        [Fact]
        public async Task CreateAsync_TrimsLeadingAndTrailingWhitespaceFromContent()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "   spaced   " };

            // Act
            var result = await _sut.CreateAsync(model, userId: "owner", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.Comment.Content.Should().Be("spaced");
        }

        [Fact]
        public async Task CreateAsync_WhenContentIsWhitespaceOnly_ReturnsContentFieldError()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "   " };

            // Act
            var result = await _sut.CreateAsync(model, userId: "owner", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.ValidationError);
            result.FieldErrors.Should().NotBeNull();
            result.FieldErrors!.Should().ContainKey(nameof(CommentCreateViewModel.Content));
        }

        [Fact]
        public async Task CreateAsync_PopulatesUserNavigationOnReturnedComment()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();
            var model = new CommentCreateViewModel { BlogId = blog.Id, Content = "hi" };

            // Act
            var result = await _sut.CreateAsync(model, userId: "owner", isAdmin: false);

            // Assert — User nav loaded so the AJAX partial can render the author.
            result.Success.Should().BeTrue();
            result.Value!.Comment.User.Should().NotBeNull();
            result.Value.Comment.User.UserName.Should().Be("alice");
        }

        // ─── DeleteAsync ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_WhenCommentDoesNotExist_ReturnsNotFound()
        {
            // Arrange — empty DB.

            // Act
            var result = await _sut.DeleteAsync(id: 999, userId: "u1", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task DeleteAsync_AsAuthor_RemovesCommentAndReturnsBlogIdAndUpdatedCount()
        {
            // Arrange
            SeedUser("author", "alice");
            var blog = SeedBlog("author");
            await _ctx.SaveChangesAsync();
            var c1 = SeedComment("author", blog.Id);
            SeedComment("author", blog.Id); // second comment so remaining count = 1
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(c1.Id, userId: "author", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.BlogId.Should().Be(blog.Id);
            result.Value.CommentCount.Should().Be(1);
            (await _ctx.Comments.FindAsync(c1.Id)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_AsStranger_ReturnsForbiddenAndKeepsComment()
        {
            // Arrange
            SeedUser("author", "alice");
            SeedUser("stranger", "bob");
            var blog = SeedBlog("author");
            await _ctx.SaveChangesAsync();
            var comment = SeedComment("author", blog.Id);
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(comment.Id, userId: "stranger", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
            (await _ctx.Comments.FindAsync(comment.Id)).Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_AsAdmin_CanDeleteAnyComment()
        {
            // Arrange
            SeedUser("author", "alice");
            SeedUser("admin", "admin");
            var blog = SeedBlog("author");
            await _ctx.SaveChangesAsync();
            var comment = SeedComment("author", blog.Id);
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(comment.Id, userId: "admin", isAdmin: true);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.CommentCount.Should().Be(0);
            (await _ctx.Comments.FindAsync(comment.Id)).Should().BeNull();
        }

        // ─── BuildDetailViewModelAsync ──────────────────────────────────

        [Fact]
        public async Task BuildDetailViewModelAsync_WhenBlogDoesNotExist_ReturnsNull()
        {
            // Arrange — empty DB.

            // Act
            var vm = await _sut.BuildDetailViewModelAsync(blogId: 999);

            // Assert
            vm.Should().BeNull();
        }

        [Fact]
        public async Task BuildDetailViewModelAsync_ReturnsBlogPagedCommentsOrderedNewestFirst()
        {
            // Arrange — 15 comments to verify page-1-of-10 cap + ordering.
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();
            for (var i = 0; i < 15; i++)
                SeedComment("owner", blog.Id, content: $"c{i}",
                    createdAt: new DateTime(2025, 1, 1, 0, 0, i, DateTimeKind.Utc));
            await _ctx.SaveChangesAsync();

            // Act
            var vm = await _sut.BuildDetailViewModelAsync(blog.Id);

            // Assert
            vm.Should().NotBeNull();
            vm!.Blog.Id.Should().Be(blog.Id);
            vm.Comments.Should().HaveCount(10);                 // page size
            vm.Comments.TotalItemCount.Should().Be(15);         // total
            // Newest-first — c14 then c13 ... c5
            vm.Comments.First().Content.Should().Be("c14");
            vm.Comments.Last().Content.Should().Be("c5");
        }

        [Fact]
        public async Task BuildDetailViewModelAsync_NewCommentViewModel_HasBlogIdPrefilled()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();

            // Act
            var vm = await _sut.BuildDetailViewModelAsync(blog.Id);

            // Assert
            vm!.NewComment.BlogId.Should().Be(blog.Id);
            vm.NewComment.Content.Should().BeEmpty();
        }
    }
}
