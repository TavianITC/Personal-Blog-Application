using Microsoft.AspNetCore.Identity;
using Personal_Blog_Application.Data.BlogApp.Data;
using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = serviceProvider.GetRequiredService<AppDbContext>();

            // Create roles if they don't exist
            string[] roles = { "ADMIN", "USER" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Create admin user if it doesn't exist
            var adminEmail = "admin@blogapp.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = "admin",
                    Email = adminEmail,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "ADMIN");
                }
            }

            // Seed 3 blog posts if there are none
            if (!context.Blogs.Any())
            {
                context.Blogs.AddRange(
                    new Blog
                    {
                        Title = "Welcome to Blog App",
                        Content = "<h2>Hello World!</h2><p>This is the first blog post.</p>",
                        Priority = 5,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = adminUser.Id
                    },
                    new Blog
                    {
                        Title = "Getting Started with ASP.NET Core",
                        Content = "<p>ASP.NET Core is a cross-platform framework...</p>",
                        Priority = 4,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = adminUser.Id
                    },
                    new Blog
                    {
                        Title = "Entity Framework Core Tips",
                        Content = "<p>EF Core makes database access easy with Code-First approach...</p>",
                        Priority = 3,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = adminUser.Id
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}
