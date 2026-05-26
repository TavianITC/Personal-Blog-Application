namespace Personal_Blog_Application.ViewModels
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "USER";
        public bool IsActive { get; set; } = true;
        public int BlogCount { get; set; }
    }
}
