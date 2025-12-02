using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Archive.Data;
using Archive.Models;
using VersOne.Epub;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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

            // 0. Ensure Cover Directory Exists
            string wwwRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string coversDir = Path.Combine(wwwRoot, "covers");
            if (!Directory.Exists(coversDir)) Directory.CreateDirectory(coversDir);

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

                        // SKIP if duplicate
                        var existingFile = await db.LibraryFiles.FirstOrDefaultAsync(f => f.FileHash == hash);
                        if (existingFile != null)
                        {
                            Console.WriteLine($"[SKIP] Duplicate Hash: {Path.GetFileName(filePath)}");
                            continue; 
                        }

                        // --- METADATA & COVER EXTRACTION ---
                        
                        var fileInfo = new FileInfo(filePath);
                        var ext = fileInfo.Extension.ToLower();

                        string title = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        string author = "Unknown";
                        string publisher = "Unknown";
                        string category = "Uncategorized";
                        string isbn = null;
                        string? coverPath = null;

                        // A. EPUB Logic
                        if (ext == ".epub")
                        {
                            try 
                            {
                                var epubBook = EpubReader.ReadBook(filePath);
                                if (!string.IsNullOrWhiteSpace(epubBook.Title)) title = epubBook.Title;
                                if (!string.IsNullOrWhiteSpace(epubBook.Author)) author = epubBook.Author;
                                
                                var meta = epubBook.Schema.Package.Metadata;
                                if (meta.Publishers.Any()) publisher = meta.Publishers.First().Publisher ?? "Unknown";
                                if (meta.Subjects.Any()) category = string.Join(", ", meta.Subjects);
                                
                                if (meta.Identifiers.Any(i => i.Scheme?.ToLower().Contains("isbn") == true))
                                    isbn = meta.Identifiers.First(i => i.Scheme?.ToLower().Contains("isbn") == true).Identifier;
                                else if (meta.Identifiers.Any())
                                    isbn = meta.Identifiers.First().Identifier;

                                // Save EPUB Cover
                                if (epubBook.CoverImage != null && epubBook.CoverImage.Length > 0)
                                {
                                    coverPath = await SaveCoverAsync(epubBook.CoverImage, hash, coversDir);
                                }
                            }
                            catch (Exception ex) { Console.WriteLine($"[WARN] Bad EPUB: {fileInfo.Name} - {ex.Message}"); }
                        }

                        // B. PDF Logic
                        else if (ext == ".pdf")
                        {
                            try
                            {
                                using (var pdf = PdfDocument.Open(filePath))
                                {
                                    var info = pdf.Information;

                                    // Metadata Extraction (Same as before)
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

                                    // --- IMPROVED COVER EXTRACTION ---
                                    // Look at the first 3 pages (covers are sometimes on page 2 or 3)
                                    int pagesCheck = Math.Min(3, pdf.NumberOfPages);

                                    for (int i = 1; i <= pagesCheck; i++)
                                    {
                                        var page = pdf.GetPage(i);
                                        var images = page.GetImages().ToList();

                                        if (images.Any())
                                        {
                                            // Find the largest image on this page
                                            var bestImage = images.OrderByDescending(img => img.Bounds.Width * img.Bounds.Height).First();

                                            // Filter: Ignore tiny icons/logos (must be at least 200x200 roughly)
                                            if (bestImage.Bounds.Width < 100 || bestImage.Bounds.Height < 100) continue;

                                            // Try to get a web-friendly format (PNG)
                                            // We DO NOT fallback to 'TryGetBytes' blindly anymore, as that often produces 
                                            // unreadable raw CMYK data that browsers can't show.
                                            if (bestImage.TryGetPng(out var pngBytes))
                                            {
                                                coverPath = await SaveCoverAsync(pngBytes, hash, coversDir);
                                                break; // Found a cover! Stop looking.
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { Console.WriteLine($"[WARN] Bad PDF: {fileInfo.Name} - {ex.Message}"); }
                        }
                        

                        // --- SAVE TO DB ---
                        var newBook = new Book
                        {
                            Title = title,
                            Author = author,
                            Publisher = publisher,
                            Category = category,
                            ISBN = isbn,
                            CoverPath = coverPath,
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

        // Helper to deduplicate the saving logic
        private async Task<string> SaveCoverAsync(byte[] imageBytes, string fileHash, string coversDir)
        {
            string coverName = $"{fileHash}.jpg"; // We save everything as .jpg for simplicity
            string physicalPath = Path.Combine(coversDir, coverName);

            if (!File.Exists(physicalPath))
            {
                await File.WriteAllBytesAsync(physicalPath, imageBytes);
            }
            return $"covers/{coverName}";
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