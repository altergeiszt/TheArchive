using Microsoft.EntityFrameworkCore;
using Archive.Data;
using Archive.Models;
using System.Text.RegularExpressions;

namespace Archive.Services
{
    public class ShelverService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ShelverService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        // Returns "Success Count"
        public async Task<int> OrganizeLibraryAsync(string targetRootPath, string sortStrategy)
        {
            if (!Directory.Exists(targetRootPath)) Directory.CreateDirectory(targetRootPath);

            int movedCount = 0;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                // Get all active files
                var files = await db.LibraryFiles
                                    .Include(f => f.Book)
                                    .Where(f => f.Status == FileStatus.Active || f.Status == FileStatus.Incoming)
                                    .ToListAsync();

                foreach (var file in files)
                {
                    if (!File.Exists(file.FilePath)) continue;

                    // 1. Determine the Subfolder Name based on Strategy
                    string subFolder = "Unsorted";
                    
                    switch (sortStrategy.ToLower())
                    {
                        case "publisher":
                            subFolder = file.Book.Publisher ?? "Unknown Publisher";
                            break;
                        case "category":
                             // Take the first category if multiple exist (e.g. "SciFi, Space" -> "SciFi")
                            subFolder = file.Book.Category?.Split(',')[0].Trim() ?? "Uncategorized";
                            break;
                        case "author":
                        default:
                            subFolder = file.Book.Author ?? "Unknown Author";
                            break;
                    }

                    // 2. Sanitize the Folder Name (Remove illegal chars like / \ : * ?)
                    subFolder = Sanitize(subFolder);
                    
                    // 3. Construct the Full Destination Path
                    // Structure: TargetRoot / StrategyFolder / Filename
                    string destFolder = Path.Combine(targetRootPath, subFolder);
                    if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

                    string destPath = Path.Combine(destFolder, Path.GetFileName(file.FilePath));

                    // 4. Move and Update DB
                    try 
                    {
                        // Prevent overwriting if file already exists there
                        if (file.FilePath != destPath) 
                        {
                            if (File.Exists(destPath))
                            {
                                // Rename collision: book.pdf -> book_1.pdf
                                string name = Path.GetFileNameWithoutExtension(destPath);
                                string ext = Path.GetExtension(destPath);
                                destPath = Path.Combine(destFolder, $"{name}_{DateTime.Now.Ticks}{ext}");
                            }

                            File.Move(file.FilePath, destPath);
                            
                            // Update Database Record
                            file.FilePath = destPath;
                            movedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to move {file.FileName}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync();
            }
            return movedCount;
        }

        private string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Unknown";
            // Regex to replace invalid chars with empty string
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(input, invalidRegStr, "");
        }
    }
}