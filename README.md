📚 The Archive
The Archive is a self-hosted ebook organizer and librarian tool built with Blazor and .NET. It scans your chaotic file folders, parses metadata from EPUB and PDF files, detects duplicates, and physically reorganizes your collection on disk.

✨ Features
1. 🔍 Intelligent Ingestion
Deep Scanning: Recursively scans local directories for .epub and .pdf files.

Metadata Extraction:

EPUB: Extracts Title, Author, Publisher, and Categories using VersOne.Epub.

PDF: Extracts metadata using PdfPig with a "Junk Filter" to ignore generic titles like "Microsoft Word - Document1".

Cover Art Mining: Automatically extracts cover images from EPUBs and scans the first 3 pages of PDFs to find the best cover candidate.

2. 👯 Duplicate Detective
Hash-Based Detection: Calculates MD5 hashes for every file to identify exact byte-for-byte duplicates instantly.

Fuzzy Grouping: Groups potential duplicates by Title and Author so you can compare formats (e.g., EPUB vs. PDF).

Safe Deletion: The "Keep This One" action does not delete files immediately. It moves "loser" files to a _Archive_Trash folder in the project root for safe recovery.

3. 🏗️ The Shelver
Physical Organization: Moves and renames your actual files on the hard drive into a clean folder structure.

Sorting Strategies: Organize your library by:

/Author/Title.epub

/Publisher/Title.epub

/Category/Title.epub

Sanitization: Automatically strips illegal characters from filenames to prevent file system errors.

4. 🎨 Modern UI (Cozy Theme)
MudBlazor: Built with Material Design components.

Responsive Grid: View your library as a grid of cards with cover art.

Search & Filter: Instant filtering by Title, Author, or Publisher.

Custom Theme: A custom "Cozy Earth" theme featuring warm browns, moss greens, and parchment backgrounds.

🛠️ Tech Stack
Framework: .NET 10.0 (Blazor Interactive Server)

Database: SQLite (Entity Framework Core)

UI Library: MudBlazor

Parsing Libraries:

VersOne.Epub (EPUB parsing)

UglyToad.PdfPig (PDF parsing)

🚀 Getting Started
Prerequisites
.NET SDK (Version compatible with .NET 10.0 as defined in Archive.csproj)

Installation
Clone the repository:

Bash

git clone https://github.com/yourusername/the-archive.git
cd the-archive
Restore dependencies:

Bash

dotnet restore
Run the application:

Bash

dotnet run
The app will automatically create the SQLite database (library.db) and the trash folder (_Archive_Trash) on the first run.

Open in Browser: Navigate to http://localhost:5xxx (check console output for the exact port).

📖 User Guide
1. Importing Books
Navigate to Home.

Enter the Absolute Path to your books folder (e.g., C:\Users\Me\Downloads\Books).

Click Start Scan. The app will populate the database and extract covers to wwwroot/covers.

2. Managing Duplicates
Navigate to Duplicates.

Click Scan for Conflicts.

Review the groups. Click "Keep This One" on the version you want to save. The other versions will be moved to the _Archive_Trash folder.

3. Organizing Files (The Shelver)
Navigate to Shelver.

Enter a Target Path (e.g., C:\My_Clean_Library).

Select a Strategy (Author, Publisher, etc.).

Click Organize Now. The app will move files from their current location to the target folder, sorted and renamed.

📂 Project Structure
Components/: Blazor UI components (Pages and Layout).

Data/: Database context (LibraryDbContext).

Models/: C# classes representing Book and LibraryFile.

Services/: Core logic engines:

LibraryScanner.cs: Handles file ingestion and metadata/cover extraction.

DuplicateService.cs: Handles logic for identifying and safely removing duplicates.

ShelverServices.cs: Handles physical file manipulation and moving.

🔮 Future Roadmap
[ ] Reader: Built-in EPUB/PDF web reader.

[ ] Metadata Editor: Manually edit incorrect titles/authors.

[ ] API Integration: Fetch metadata from Google Books/Open Library.

[ ] User Accounts: Multi-user support.
