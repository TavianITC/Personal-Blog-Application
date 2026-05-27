using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.Tests.Helpers
{
    // Full Identity stack wired against SQLite in-memory — UserManager,
    // RoleManager, and AppDbContext all backed by the same connection-scoped
    // DB. Use for tests that need real Identity behaviour (password hashing,
    // role assignments, normalized lookups, etc.). For pure DbContext tests
    // see SqliteTestContext.
    public sealed class IdentityTestContext : IDisposable
    {
        public AppDbContext Db { get; }
        public UserManager<User> Users { get; }
        public RoleManager<IdentityRole> Roles { get; }

        private readonly ServiceProvider _services;
        private readonly IServiceScope _scope;
        private readonly SqliteConnection _connection;

        public IdentityTestContext()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            services
                .AddIdentity<User, IdentityRole>(o =>
                {
                    // Mirror production policy (Program.cs).
                    o.Password.RequireDigit = true;
                    o.Password.RequiredLength = 6;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Password.RequireUppercase = false;
                })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();
            services.AddHttpContextAccessor();

            _services = services.BuildServiceProvider();
            _scope = _services.CreateScope();

            Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();
            Users = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        }

        // Roles must exist before AddToRoleAsync is called.
        public async Task SeedRolesAsync()
        {
            foreach (var role in new[] { "USER", "ADMIN" })
            {
                if (!await Roles.RoleExistsAsync(role))
                    await Roles.CreateAsync(new IdentityRole(role));
            }
        }

        public async Task<User> CreateUserAsync(
            string userName, string email, string password,
            string role = "USER", bool isActive = true)
        {
            var user = new User { UserName = userName, Email = email, IsActive = isActive };
            var create = await Users.CreateAsync(user, password);
            if (!create.Succeeded)
                throw new InvalidOperationException(
                    "Seed CreateUser failed: " +
                    string.Join("; ", create.Errors.Select(e => e.Description)));
            if (!string.IsNullOrEmpty(role))
                await Users.AddToRoleAsync(user, role);
            return user;
        }

        public void Dispose()
        {
            _scope.Dispose();
            _services.Dispose();
            _connection.Dispose();
        }
    }
}
