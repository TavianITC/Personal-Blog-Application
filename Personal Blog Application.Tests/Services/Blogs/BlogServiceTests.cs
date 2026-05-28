using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Blogs;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.Tests.Helpers;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Tests.Services.Blogs
{
    // Full coverage of BlogService against SQLite in-memory.
    // Every test gets a fresh DB via the constructor; Dispose tears it down.
    public class BlogServiceTests : IDisposable
    {
        private readonly SqliteTestContext _fixture;
        private readonly AppDbContext _ctx;
        private readonly BlogService _sut;

        public BlogServiceTests()
        {
            _fixture = new SqliteTestContext();
            _ctx = _fixture.Db;
            _sut = new BlogService(_ctx);
        }

        public void Dispose() => _fixture.Dispose();

        // Seed helpers live in Helpers/Seed.cs as AppDbContext extension methods.
        // Local thin wrappers keep call sites tidy.
        private User SeedUser(string id, string userName) =>
            _ctx.SeedUser(id, userName);

        private Blog SeedBlog(
            string userId,
            string title = "T", string status = "PUBLISHED",
            int priority = 1, string content = "C",
            DateTime? createdAt = null) =>
            _ctx.SeedBlog(userId, title, status, priority, content, createdAt);

        private Comment SeedComment(string userId, int blogId, DateTime? createdAt = null) =>
            _ctx.SeedComment(userId, blogId, "comment", createdAt);

        // ─── GetFeedAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetFeedAsync_AsRegularUser_ReturnsOnlyPublishedPosts()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "Pub", status: "PUBLISHED");
            SeedBlog("u1", title: "Drft", status: "DRAFT");
            SeedBlog("u1", title: "Priv", status: "PRIVATE");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: null, author: null, sort: null, priority: null,
                userId: "viewer", isAdmin: false);

            // Assert
            page.Should().ContainSingle().Which.Title.Should().Be("Pub");
        }

        [Fact]
        public async Task GetFeedAsync_AsAdmin_ReturnsPostsOfEveryStatus()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "Pub", status: "PUBLISHED");
            SeedBlog("u1", title: "Drft", status: "DRAFT");
            SeedBlog("u1", title: "Priv", status: "PRIVATE");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: null, author: null, sort: null, priority: null,
                userId: "admin", isAdmin: true);

            // Assert
            page.TotalItemCount.Should().Be(3);
            page.Select(b => b.Status).Should().BeEquivalentTo(new[] { "PUBLISHED", "DRAFT", "PRIVATE" });
        }

        [Fact]
        public async Task GetFeedAsync_SearchMatchesTitleOrContent_CaseInsensitive()
        {
            // Arrange — case differs intentionally to verify LIKE collation.
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "Hello World", content: "first", status: "PUBLISHED");
            SeedBlog("u1", title: "Another title", content: "Says hELLo there", status: "PUBLISHED");
            SeedBlog("u1", title: "Goodbye", content: "nothing", status: "PUBLISHED");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: "hello", author: null, sort: null, priority: null,
                userId: "viewer", isAdmin: false);

            // Assert
            page.Select(b => b.Title).Should().BeEquivalentTo(new[] { "Hello World", "Another title" });
        }

        [Fact]
        public async Task GetFeedAsync_AuthorFilter_LimitsResultsToMatchingAuthorName()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedUser("u2", "bob");
            SeedBlog("u1", title: "by alice", status: "PUBLISHED");
            SeedBlog("u2", title: "by bob", status: "PUBLISHED");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: null, author: "ali", sort: null, priority: null,
                userId: "viewer", isAdmin: false);

            // Assert
            page.Should().ContainSingle().Which.Title.Should().Be("by alice");
        }

        [Fact]
        public async Task GetFeedAsync_PriorityFilter_ReturnsOnlyMatchingPriority()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "P1", priority: 1);
            SeedBlog("u1", title: "P3", priority: 3);
            SeedBlog("u1", title: "P5", priority: 5);
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: null, author: null, sort: null, priority: 3,
                userId: "viewer", isAdmin: false);

            // Assert
            page.Should().ContainSingle().Which.Priority.Should().Be(3);
        }

        [Fact]
        public async Task GetFeedAsync_SortByPriorityDesc_OrdersHighToLow()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "P2", priority: 2);
            SeedBlog("u1", title: "P5", priority: 5);
            SeedBlog("u1", title: "P1", priority: 1);
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetFeedAsync(
                search: null, author: null, sort: "priority_desc", priority: null,
                userId: "viewer", isAdmin: false);

            // Assert
            page.Select(b => b.Priority).Should().Equal(5, 2, 1);
        }

        [Fact]
        public async Task GetFeedAsync_PaginatesAtTheDatabase()
        {
            // Arrange — 5 blogs with distinct timestamps so order is stable.
            SeedUser("u1", "alice");
            for (var i = 0; i < 5; i++)
                SeedBlog("u1", title: $"B{i}", createdAt: new DateTime(2025, 1, 1, 0, 0, i, DateTimeKind.Utc));
            await _ctx.SaveChangesAsync();

            // Act — page 2, size 2, default sort = CreatedAt desc.
            var page = await _sut.GetFeedAsync(
                search: null, author: null, sort: null, priority: null,
                userId: "viewer", isAdmin: false,
                page: 2, pageSize: 2);

            // Assert — newest-first ordering: B4,B3,B2,B1,B0 → page 2 is B2,B1.
            page.PageNumber.Should().Be(2);
            page.TotalItemCount.Should().Be(5);
            page.Select(b => b.Title).Should().Equal("B2", "B1");
        }

        // ─── GetMineAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetMineAsync_ReturnsCallerPostsAcrossAllStatuses_AndExcludesOthers()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedUser("u2", "bob");
            SeedBlog("u1", title: "mine published", status: "PUBLISHED");
            SeedBlog("u1", title: "mine draft", status: "DRAFT");
            SeedBlog("u1", title: "mine private", status: "PRIVATE");
            SeedBlog("u2", title: "theirs", status: "PUBLISHED");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetMineAsync(
                search: null, sort: null, status: null, priority: null,
                userId: "u1");

            // Assert
            page.TotalItemCount.Should().Be(3);
            page.Select(b => b.Title).Should().BeEquivalentTo(new[]
            {
                "mine published", "mine draft", "mine private"
            });
        }

        [Fact]
        public async Task GetMineAsync_StatusFilter_LimitsToThatBucket()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", title: "p", status: "PUBLISHED");
            SeedBlog("u1", title: "d", status: "DRAFT");
            SeedBlog("u1", title: "x", status: "PRIVATE");
            await _ctx.SaveChangesAsync();

            // Act
            var page = await _sut.GetMineAsync(
                search: null, sort: null, status: "DRAFT", priority: null,
                userId: "u1");

            // Assert
            page.Should().ContainSingle().Which.Status.Should().Be("DRAFT");
        }

        // ─── GetMyStatusCountsAsync ─────────────────────────────────────

        [Fact]
        public async Task GetMyStatusCountsAsync_GroupsByStatus_AndFillsMissingWithZero()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedBlog("u1", status: "PUBLISHED");
            SeedBlog("u1", status: "PUBLISHED");
            SeedBlog("u1", status: "DRAFT");
            // No PRIVATE → ensure the method still returns 0 for it.
            await _ctx.SaveChangesAsync();

            // Act
            var counts = await _sut.GetMyStatusCountsAsync("u1");

            // Assert
            counts["PUBLISHED"].Should().Be(2);
            counts["DRAFT"].Should().Be(1);
            counts["PRIVATE"].Should().Be(0);
        }

        // ─── GetHomeFeedAsync ───────────────────────────────────────────

        [Fact]
        public async Task GetHomeFeedAsync_ReturnsOnlyPublishedExcludingCaller_OrderedNewestFirst()
        {
            // Arrange
            SeedUser("u1", "alice");
            SeedUser("u2", "bob");
            SeedBlog("u1", title: "my own", status: "PUBLISHED",
                createdAt: new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc));
            SeedBlog("u2", title: "theirs new", status: "PUBLISHED",
                createdAt: new DateTime(2025, 1, 4, 0, 0, 0, DateTimeKind.Utc));
            SeedBlog("u2", title: "theirs old", status: "PUBLISHED",
                createdAt: new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            SeedBlog("u2", title: "theirs draft", status: "DRAFT",
                createdAt: new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc));
            await _ctx.SaveChangesAsync();

            // Act
            var feed = await _sut.GetHomeFeedAsync(userId: "u1");

            // Assert
            feed.Select(b => b.Title).Should().Equal("theirs new", "theirs old");
        }

        [Fact]
        public async Task GetHomeFeedAsync_RespectsTakeLimit()
        {
            // Arrange — 5 published by someone else, ask for top 2.
            SeedUser("u2", "bob");
            for (var i = 0; i < 5; i++)
                SeedBlog("u2", title: $"B{i}", status: "PUBLISHED",
                    createdAt: new DateTime(2025, 1, 1, 0, 0, i, DateTimeKind.Utc));
            await _ctx.SaveChangesAsync();

            // Act
            var feed = await _sut.GetHomeFeedAsync(userId: "u1", take: 2);

            // Assert
            feed.Should().HaveCount(2);
            feed.Select(b => b.Title).Should().Equal("B4", "B3");
        }

        // ─── GetDetailAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetDetailAsync_WhenBlogDoesNotExist_ReturnsNotFound()
        {
            // Arrange — empty DB.

            // Act
            var result = await _sut.GetDetailAsync(
                id: 999, userId: "viewer", isAdmin: false, from: null);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        [Fact]
        public async Task GetDetailAsync_WhenDraftAndCallerIsStranger_ReturnsForbidden()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", status: "DRAFT");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.GetDetailAsync(blog.Id, "stranger", isAdmin: false, from: null);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
        }

        [Fact]
        public async Task GetDetailAsync_WhenDraftAndCallerIsOwner_ReturnsBlog()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", title: "Drafty", status: "DRAFT");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.GetDetailAsync(blog.Id, "owner", isAdmin: false, from: null);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.Blog.Title.Should().Be("Drafty");
        }

        [Fact]
        public async Task GetDetailAsync_PaginatesCommentsAtDatabase()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", status: "PUBLISHED");
            await _ctx.SaveChangesAsync();
            for (var i = 0; i < 15; i++)
                SeedComment("owner", blog.Id,
                    createdAt: new DateTime(2025, 1, 1, 0, 0, i, DateTimeKind.Utc));
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.GetDetailAsync(
                blog.Id, "owner", isAdmin: false, from: null,
                commentPage: 1, commentPageSize: 10);

            // Assert
            result.Success.Should().BeTrue();
            result.Value!.Comments.Should().HaveCount(10);
            result.Value.Comments.TotalItemCount.Should().Be(15);
        }

        // ─── GetForEditAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetForEditAsync_AsOwner_ReturnsViewModelMappedFromBlog()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", title: "T", content: "C",
                priority: 4, status: "PRIVATE");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.GetForEditAsync(blog.Id, "owner", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new BlogCreateViewModel
            {
                Title = "T", Content = "C", Priority = 4, Status = "PRIVATE"
            });
        }

        [Fact]
        public async Task GetForEditAsync_AsStranger_ReturnsForbidden()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.GetForEditAsync(blog.Id, "stranger", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
        }

        // ─── CreateAsync ────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_PersistsBlogToDatabaseAndReturnsOutcome()
        {
            // Arrange
            SeedUser("u1", "alice");
            await _ctx.SaveChangesAsync();
            var model = new BlogCreateViewModel
            {
                Title = "Hello",
                Content = "<p>x</p>",
                Priority = 3,
                Status = "PUBLISHED"
            };

            // Act
            var result = await _sut.CreateAsync(model, userId: "u1");

            // Assert
            result.Success.Should().BeTrue();
            var saved = await _ctx.Blogs.FindAsync(result.Value!.Id);
            saved.Should().NotBeNull();
            saved!.Title.Should().Be("Hello");
            saved.CreatedBy.Should().Be("u1");
        }

        // ─── UpdateAsync ────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_AsOwner_UpdatesFieldsAndSetsUpdatedAt()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", title: "Old", status: "DRAFT");
            await _ctx.SaveChangesAsync();
            var model = new BlogCreateViewModel
            {
                Title = "New",
                Content = "<p>n</p>",
                Priority = 5,
                Status = "PUBLISHED"
            };

            // Act
            var result = await _sut.UpdateAsync(blog.Id, model, "owner", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            var updated = await _ctx.Blogs.FindAsync(blog.Id);
            updated!.Title.Should().Be("New");
            updated.Status.Should().Be("PUBLISHED");
            updated.Priority.Should().Be(5);
            updated.UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateAsync_AsStranger_ReturnsForbiddenAndKeepsBlogIntact()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", title: "Untouched");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.UpdateAsync(blog.Id, new BlogCreateViewModel
            {
                Title = "Hacked", Content = "x", Priority = 1, Status = "PUBLISHED"
            }, userId: "stranger", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
            var unchanged = await _ctx.Blogs.FindAsync(blog.Id);
            unchanged!.Title.Should().Be("Untouched");
        }

        [Fact]
        public async Task UpdateAsync_AsAdmin_CanUpdateAnyoneElsesBlog()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner", title: "Old");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.UpdateAsync(blog.Id, new BlogCreateViewModel
            {
                Title = "Admin edit", Content = "c", Priority = 1, Status = "PUBLISHED"
            }, userId: "admin", isAdmin: true);

            // Assert
            result.Success.Should().BeTrue();
            var updated = await _ctx.Blogs.FindAsync(blog.Id);
            updated!.Title.Should().Be("Admin edit");
        }

        [Fact]
        public async Task UpdateAsync_WhenBlogNotFound_ReturnsNotFound()
        {
            // Arrange — empty DB.

            // Act
            var result = await _sut.UpdateAsync(999, new BlogCreateViewModel
            {
                Title = "x", Content = "x", Priority = 1, Status = "DRAFT"
            }, userId: "u1", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }

        // ─── DeleteAsync ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_AsOwner_RemovesBlogFromDatabase()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(blog.Id, "owner", isAdmin: false);

            // Assert
            result.Success.Should().BeTrue();
            (await _ctx.Blogs.FindAsync(blog.Id)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_AsStranger_ReturnsForbiddenAndKeepsBlog()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(blog.Id, "stranger", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.Forbidden);
            (await _ctx.Blogs.FindAsync(blog.Id)).Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_AsAdmin_CanRemoveAnyoneElsesBlog()
        {
            // Arrange
            SeedUser("owner", "alice");
            var blog = SeedBlog("owner");
            await _ctx.SaveChangesAsync();

            // Act
            var result = await _sut.DeleteAsync(blog.Id, "admin", isAdmin: true);

            // Assert
            result.Success.Should().BeTrue();
            (await _ctx.Blogs.FindAsync(blog.Id)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_WhenBlogNotFound_ReturnsNotFound()
        {
            // Arrange — empty DB.

            // Act
            var result = await _sut.DeleteAsync(999, "u1", isAdmin: false);

            // Assert
            result.Status.Should().Be(ResultStatus.NotFound);
        }
    }
}
