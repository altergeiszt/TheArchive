using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Archive.Data;
using Archive.Models;

namespace Archive.Services
{
    public class LibraryScanner
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public LibraryScanner(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

    public async Task ScanDirectoryAsync(string rootPath)
        {
            // Valid file extensions to look for
            var allowedExtensions = new[]
            {
                ".epub", ".pdf", ".mobi"
            };
            // Recursively walk through the directories.
            var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLower()));
            
            //Create a new scope for database access
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                foreach (var filePath in files)
                {
                    // Hash Calculation, 
                    var hash = CalculateHash(filePath);

                    // Check for exact duplicate
                    var existingFile = await db.LibraryFiles.FirstOrDefaultAsync(file => file.FileHash == hash);
                    
                    if (existingFile != null)
                    {
                        // Exact Duplicate, log or skip
                        Console.WriteLine($"[DUPLICATE] {Path.GetFileName(filePath)} matches {existingFile.FileName}");
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);

                    var newBook = new Book
                    {
                        Title = Path.GetFileNameWithoutExtension(fileInfo.Name),
                        Author = "Unknown",
                        CreatedAt = DateTime.Now
                    };

                    var newLibraryFile = new LibraryFile
                    {
                        Book = newBook,
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        Extension = fileInfo.Extension.ToLower(),
                        FileHash = hash,
                        SizeBytes = fileInfo.Length,
                        Status = FileStatus.Incoming
                    };

                    db.Books.Add(newBook);
                    db.LibraryFiles.Add(newLibraryFile);

                    await db.SaveChangesAsync();
                    Console.WriteLine($"[ADDED] {fileInfo.Name}");
                }
            }
        }  
        private string CalculateHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}