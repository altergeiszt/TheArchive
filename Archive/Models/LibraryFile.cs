using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Archive.Models
{
    public enum FileStatus
    {
        Active,         // The main copy we show in the library
        Duplicate,      // A copy we intend to delete later
        Quarantine,     // File is corrupted or unreadable
        Incoming        // Just scanned, waiting for processing
    }

    public class LibraryFile
    {
        [Key]
        public int Id { get; set; }

        // Link back to the parent Book
        public int BookId { get; set; }
        public Book? Book { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public string FileName { get; set; } = string.Empty;

        // This is your SHA-256 Fingerprint
        [MaxLength(64)] 
        public string FileHash { get; set; } = string.Empty;

        // e.g., ".pdf", ".epub"
        [MaxLength(10)]
        public string Extension { get; set; } = string.Empty;

        // Size in bytes (Use this to prefer larger/better quality files)
        public long SizeBytes { get; set; }

        public FileStatus Status { get; set; } = FileStatus.Incoming;
    }
}