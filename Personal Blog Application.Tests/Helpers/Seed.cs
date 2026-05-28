using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.Tests.Helpers
{
    // Shared DbContext seeding helpers. Each method only adds the entity to
    // the change tracker — the test decides when to call SaveChangesAsync.
    public static class Seed
    {
        public static User SeedUser(
            this AppDbContext ctx,
            string id,
            string userName,
            string? email = null,
            bool isActive = true)
        {
            var u = new User
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email?.ToUpperInvariant(),
                IsActive = isActive
            };
            ctx.Users.Add(u);
            return u;
        }

        public static Blog SeedBlog(
            this AppDbContext ctx,
            string userId,
            string title = "T",
            string status = "PUBLISHED",
            int priority = 1,
            string content = "C",
            DateTime? createdAt = null)
        {
            var b = new Blog
            {
                Title = title,
                Content = content,
                Status = status,
                Priority = priority,
                CreatedBy = userId,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            ctx.Blogs.Add(b);
            return b;
        }

        public static Comment SeedComment(
            this AppDbContext ctx,
            string userId,
            int blogId,
            string content = "x",
            DateTime? createdAt = null)
        {
            var c = new Comment
            {
                BlogId = blogId,
                Content = content,
                CreatedBy = userId,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            ctx.Comments.Add(c);
            return c;
        }
    }
}
