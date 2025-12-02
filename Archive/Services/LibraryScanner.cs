using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Archive.Data;
using Archive.Models;
using VersOne.Epub;
using UglyToad.PdfPig;

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
            var allowedExtensions = new[] { ".epub", ".pdf", ".mobi", ".txt" };

            // 1. Find all files
            var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLower()));

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                foreach (var filePath in files)
                {
                    try 
                    {
                        var hash = CalculateHash(filePath);

                        // SKIP if we already have this specific file
                        var existingFile = await db.LibraryFiles.FirstOrDefaultAsync(f => f.FileHash == hash);
                        if (existingFile != null)
                        {
                            Console.WriteLine($"[SKIP] Duplicate Hash: {Path.GetFileName(filePath)}");
                            continue; 
                        }

                        // --- METADATA EXTRACTION ---
                        
                        var fileInfo = new FileInfo(filePath);
                        var ext = fileInfo.Extension.ToLower();

                        // Defaults
                        string title = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        string author = "Unknown";
                        string publisher = "Unknown";
                        string category = "Uncategorized";
                        string isbn = null;

                        // A. EPUB Logic
                        if (ext == ".epub")
                        {
                            try 
                            {
                                var epubBook = EpubReader.ReadBook(filePath);
                                
                                if (!string.IsNullOrWhiteSpace(epubBook.Title)) title = epubBook.Title;
                                if (!string.IsNullOrWhiteSpace(epubBook.Author)) author = epubBook.Author;
                                
                                // Extract Publisher & Category (For the Shelver)
                                var meta = epubBook.Schema.Package.Metadata;
                                if (meta.Publishers.Any()) 
                                    publisher = meta.Publishers.First().Publisher ?? "Unknownd";

                                if (meta.Subjects.Any())
                                    category = string.Join(", ", meta.Subjects);
                                
                                // Extract ISBN
                                if (meta.Identifiers.Any(i => i.Scheme?.ToLower().Contains("isbn") == true))
                                    isbn = meta.Identifiers.First(i => i.Scheme?.ToLower().Contains("isbn") == true).Identifier;
                                else if (meta.Identifiers.Any())
                                    isbn = meta.Identifiers.First().Identifier;
                            }
                            catch (Exception ex) { Console.WriteLine($"[WARN] Bad EPUB: {fileInfo.Name} - {ex.Message}"); 
                            }
                        }
                        // B. PDF Logic
                        else if (ext == ".pdf")
                        {
                            try
                            {
                                using (var pdf = PdfDocument.Open(filePath))
                                {
                                    var info = pdf.Information;
                                    
                                    // Basic Junk Filter
                                    if (!string.IsNullOrWhiteSpace(info.Title))
                                    {
                                        if (!info.Title.Contains("Microsoft Word") && !info.Title.Contains("Untitled"))
                                            title = info.Title;
                                    }

                                    if (!string.IsNullOrWhiteSpace(info.Author))
                                    {
                                        if (!info.Author.Contains("Administrator") && !info.Author.Contains("Print to PDF"))
                                            author = info.Author;
                                    }
                                }
                            }
                            catch (Exception ex) { Console.WriteLine($"[WARN] Bad PDF: {fileInfo.Name} - {ex.Message}"); 
                            }
                        }

                        // --- SAVE TO DB ---
                        
                        var newBook = new Book
                        {
                            Title = title,
                            Author = author,
                            Publisher = publisher,
                            Category = category,
                            ISBN = isbn,
                            CreatedAt = DateTime.Now
                        };

                        var newLibraryFile = new LibraryFile
                        {
                            Book = newBook,
                            FilePath = filePath,
                            FileName = fileInfo.Name,
                            Extension = ext,
                            FileHash = hash,
                            SizeBytes = fileInfo.Length,
                            Status = FileStatus.Incoming
                        };

                        db.Books.Add(newBook);
                        db.LibraryFiles.Add(newLibraryFile);
                        
                        // Saving per file is slower but safer for debugging
                        await db.SaveChangesAsync();
                        Console.WriteLine($"[ADDED] {title}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed processing {filePath}: {ex.Message}");
                    }
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