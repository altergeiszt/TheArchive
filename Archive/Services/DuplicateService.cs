using Microsoft.EntityFrameworkCore;
using Archive.Data;
using Archive.Models;

namespace Archive.Services
{
    public class DuplicateService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _trashPath;

        public DuplicateService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            
            // FIX: Use GetCurrentDirectory() to put the folder in your Project Root
            // This makes it visible in VS Code/File Explorer immediately.
            _trashPath = Path.Combine(Directory.GetCurrentDirectory(), "_Archive_Trash");

            // FIX: Create the directory immediately on startup/injection
            // so you can see it exists even before you delete anything.
            if (!Directory.Exists(_trashPath))
            {
                Directory.CreateDirectory(_trashPath);
            }
        }

        public async Task<List<List<Book>>> GetPotentialDuplicatesAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                
                // Fetch books, ignoring 'Unknown' or empty titles
                var allBooks = await db.Books
                                       .Include(b => b.Files)
                                       .Where(b => b.Title != "Unknown" && b.Title != "")
                                       .ToListAsync();

                return allBooks.GroupBy(b => GenerateKey(b))
                                      .Where(g => g.Count() > 1)
                                      .Select(g => g.ToList())
                                      .ToList();
            }
        }

        public async Task ResolveDuplicateAsync(int keepBookId, List<int> deleteBookIds)
        {
            // Double check existence just in case user deleted the folder manually
            if (!Directory.Exists(_trashPath)) Directory.CreateDirectory(_trashPath);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                var filesToMove = await db.LibraryFiles
                                            .Where(f => deleteBookIds.Contains(f.BookId))
                                            .ToListAsync();

                foreach (var file in filesToMove)
                {
                    if (File.Exists(file.FilePath))
                    {
                        try 
                        {
                            var fileInfo = new FileInfo(file.FilePath);
                            // Safety: Add timestamp to filename to prevent overwrites
                            string safeName = $"{DateTime.Now.Ticks}_{fileInfo.Name}";
                            string destPath = Path.Combine(_trashPath, safeName);

                            File.Move(file.FilePath, destPath);
                            Console.WriteLine($"[MOVED TO TRASH] {file.FilePath} -> {destPath}");
                        }
                        catch (Exception ex) 
                        { 
                            Console.WriteLine($"[ERROR] Could not move file: {ex.Message}");
                            continue; 
                        }
                    }
                }

                var booksToDelete = await db.Books
                                            .Where(b => deleteBookIds.Contains(b.Id))
                                            .ToListAsync();

                db.Books.RemoveRange(booksToDelete);
                await db.SaveChangesAsync();
            }
        }

        private string GenerateKey(Book b)
        {
            var t = b.Title?.ToLowerInvariant().Trim() ?? "";
            var a = b.Author?.ToLowerInvariant().Trim() ?? "";
            return $"{t}|{a}";
        }
    }
}