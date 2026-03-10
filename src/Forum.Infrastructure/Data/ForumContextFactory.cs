using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forum.Infrastructure.Data;

/// <summary>
/// Factory used by EF Core CLI tools (e.g. dotnet ef migrations)
/// to create a ForumDbContext when the application host is not running.
/// </summary>
public class ForumDbContextFactory
    : IDesignTimeDbContextFactory<ForumDbContext>
{
    public ForumDbContext CreateDbContext(string[] args)
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        var dbPath = Path.Combine(solutionRoot, "forum.db");

        var optionsBuilder = new DbContextOptionsBuilder<ForumDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new ForumDbContext(optionsBuilder.Options);
    }
}
