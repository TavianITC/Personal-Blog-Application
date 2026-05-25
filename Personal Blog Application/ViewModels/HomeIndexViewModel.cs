using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<Blog> Feed { get; set; } = new();
    }
}
