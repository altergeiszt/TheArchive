using Microsoft.EntityFrameworkCore;
using Archive.Components;
using Archive.Data; // Ensure this matches your namespace

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(); // Enables the interactive UI

// 2. REGISTER YOUR DATABASE HERE
// We read the "DefaultConnection" string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<LibraryDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddScoped<Archive.Services.LibraryScanner>();
builder.Services.AddScoped<Archive.Services.DuplicateService>();
builder.Services.AddScoped<Archive.Services.ShelverService>();

var app = builder.Build();


// 3. AUTOMATIC DATABASE CREATION (MVP Only)
// This checks if library.db exists. If not, it creates it based on your Models.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    // This creates the DB if it doesn't exist
    dbContext.Database.EnsureCreated();
}

// 4. Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // Allows serving CSS/JS from wwwroot
app.UseAntiforgery();

// 5. Map the UI Components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(); // Critical for button clicks to work!

app.Run();