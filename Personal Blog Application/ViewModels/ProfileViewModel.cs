namespace Personal_Blog_Application.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "USER";
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
        public int BlogCount { get; set; }
        public int CommentCount { get; set; }
    }
}
