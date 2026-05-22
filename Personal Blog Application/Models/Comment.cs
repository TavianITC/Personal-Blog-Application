using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        //FK
        public int BlogId { get; set; }
        public Blog Blog { get; set; } = null!;

        public string CreatedBy { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}
