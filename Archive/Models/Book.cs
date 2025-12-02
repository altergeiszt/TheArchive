using System.ComponentModel.DataAnnotations;

namespace Archive.Models
{
    public class Book
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Author { get; set; } = "Unknown";

        [MaxLength(100)]
        public string Publisher { get; set; } = "Unknown";  // <--- NEW

        [MaxLength(200)]
        public string Category { get; set; } = "Uncategorized"; // <--- NEW (Keywords/Subjects)

        [MaxLength(20)]
        public string? ISBN { get; set; }

        public string? CoverPath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<LibraryFile> Files { get; set; } = new List<LibraryFile>();
    }
}