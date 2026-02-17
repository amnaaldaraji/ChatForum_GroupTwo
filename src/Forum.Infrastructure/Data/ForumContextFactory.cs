using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forum.Infrastructure.Data;

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
