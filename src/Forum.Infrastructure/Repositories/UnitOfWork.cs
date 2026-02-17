using Forum.Application.Common.Interfaces;
using Forum.Infrastructure.Data;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Implements the Unit of Work pattern by wrapping ForumDbContext.SaveChangesAsync.
/// Ensures that all repository operations within a single request are committed as a single transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ForumDbContext _context;

    /// <summary>
    /// Initializes a new instance of the UnitOfWork class.
    /// </summary>
    public UnitOfWork(ForumDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Persists all pending changes tracked by the underlying ForumDbContext to the database,
    /// in a single transaction.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}