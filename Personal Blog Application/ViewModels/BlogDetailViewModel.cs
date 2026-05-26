using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.ViewModels
{
    public class BlogDetailViewModel
    {
        public Blog Blog { get; set; } = null!;
        public List<Comment> Comments { get; set; } = new();
        public CommentCreateViewModel NewComment { get; set; } = new();
    }
}
