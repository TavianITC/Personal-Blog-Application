using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.ViewModels
{
    public class CommentCreateViewModel
    {
        [Required]
        public int BlogId { get; set; }

        [Required(ErrorMessage = "Comment cannot be empty.")]
        [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
        public string Content { get; set; } = string.Empty;

        // Forwards the ?from=mine query so Detail's back link survives the round-trip
        public string? From { get; set; }
    }
}
