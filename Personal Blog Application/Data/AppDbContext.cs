using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.Data
{

    namespace BlogApp.Data
    {
        public class AppDbContext : IdentityDbContext<User>
        {
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public DbSet<Blog> Blogs { get; set; }
            public DbSet<Comment> Comments { get; set; }

            protected override void OnModelCreating(ModelBuilder builder)
            {
                base.OnModelCreating(builder); //call base method to ensure Identity configurations are applied

                // User -> Blogs (1 - many)
                builder.Entity<Blog>()
                    .HasOne(b => b.User)
                    .WithMany(u => u.Blogs)
                    .HasForeignKey(b => b.CreatedBy)
                    .OnDelete(DeleteBehavior.Cascade);

                // User -> Comments (1 - many)
                builder.Entity<Comment>()
                    .HasOne(c => c.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                // Blog -> Comments (1 - many)
                builder.Entity<Comment>()
                    .HasOne(c => c.Blog)
                    .WithMany(b => b.Comments)
                    .HasForeignKey(c => c.BlogId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}