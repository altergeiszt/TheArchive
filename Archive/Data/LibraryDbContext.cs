using Microsoft.EntityFrameworkCore;
using Archive.Models;

namespace Archive.Data
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) 
            : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<LibraryFile> LibraryFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure the ISBN is indexed so searching is fast
            modelBuilder.Entity<Book>()
                .HasIndex(b => b.ISBN);

            // Ensure we can quickly look up files by Hash to find duplicates
            modelBuilder.Entity<LibraryFile>()
                .HasIndex(f => f.FileHash);
        }
    }
}