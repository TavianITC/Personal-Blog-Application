using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.ViewModels
{
    public class BlogListViewModel
    {
        public List<Blog> Blogs { get; set; } = new();
        public string? SearchQuery { get; set; }
        public int? PriorityFilter { get; set; }
        public string? SortOrder { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }
}
