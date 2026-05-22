using Microsoft.AspNetCore.Identity;

namespace Personal_Blog_Application.Models
{
    public class User : IdentityUser
    {
        // inherit properties from IdentityUser, such as Id, UserName, Email, PasswordHash, etc.

        // Add additional properties
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Blog> Blogs { get; set; } = new List<Blog>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
