using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.Models
{
    public class Blog
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Priority { get; set; }

        public string Status { get; set; } = "DRAFT"; // DRAFT, PUBLISHED, PRIVATE

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        //FK
        public string CreatedBy { get; set; } = string.Empty;

        public User User { get; set; } = null!;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();


    }
}
