using Personal_Blog_Application.Models;
using X.PagedList;

namespace Personal_Blog_Application.ViewModels
{
    public class BlogDetailViewModel
    {
        public Blog Blog { get; set; } = null!;

        // Paged so the Detail view can render only a page of comments at a time.
        // Use Comments.TotalItemCount for the badge / "X comments" header.
        public IPagedList<Comment> Comments { get; set; } =
            new StaticPagedList<Comment>(Array.Empty<Comment>(), 1, 10, 0);

        public CommentCreateViewModel NewComment { get; set; } = new();
    }
}
