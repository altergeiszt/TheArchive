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

        // We allow this to be null because many PDFs won't have an ISBN
        [MaxLength(20)]
        public string? ISBN { get; set; }

        // Path to a generated thumbnail image for the UI
        public string? CoverPath { get; set; }

        // Useful for sorting by "Recently Added"
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationship: One Book can have many physical files (PDF, EPUB, Duplicate)
        public List<LibraryFile> Files { get; set; } = new List<LibraryFile>();
    }
}