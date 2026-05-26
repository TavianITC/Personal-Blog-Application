using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.ViewModels
{
    public class UserEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(64, MinimumLength = 3, ErrorMessage = "Username must be 3–64 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9._@+\-]+$",
            ErrorMessage = "Username may only contain letters, digits, and . _ @ + -")]
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("ADMIN|USER", ErrorMessage = "Role must be ADMIN or USER.")]
        public string Role { get; set; } = "USER";

        public bool IsActive { get; set; } = true;
    }
}
