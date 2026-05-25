using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.ViewModels
{
    public class BlogCreateViewModel
    {

        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(500, ErrorMessage = "Title cannot exceed 500 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required.")]
        [MinLength(1, ErrorMessage = "Content cannot be empty.")]
        public string Content { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "Priority must be between 1 and 5.")]
        public int Priority { get; set; } = 1;

        [Required]
        [RegularExpression("^(DRAFT|PUBLISHED|PRIVATE)$",
            ErrorMessage = "Status must be DRAFT, PUBLISHED, or PRIVATE.")]
        public string Status { get; set; } = "DRAFT";
    }
}
