using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forum.Infrastructure.Data;

public class ForumDbContextFactory
    : IDesignTimeDbContextFactory<ForumDbContext>
{
    public ForumDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ForumDbContext>();

        optionsBuilder.UseSqlite("Data Source=forum.db");

        return new ForumDbContext(optionsBuilder.Options);
    }
}
