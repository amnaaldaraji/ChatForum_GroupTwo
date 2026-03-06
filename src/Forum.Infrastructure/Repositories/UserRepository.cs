using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for User entities.
/// Extending the generic Repository{T} with user-specific query methods.
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    /// <summary>
    /// Initializes a new instance of the UserRepository class.
    /// </summary>
    public UserRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves all users with their threads and comments eagerly loaded for count display.
    /// </summary>
    public override async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by their ID with their threads and comments eagerly loaded.
    /// For user profile views that display activity statistics.
    /// </summary>
    public async Task<User?> GetByIdWithDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    /// <summary>
    /// Checks whether a username is already taken, optionally excluding a specific user
    /// (when a user is updating their username).
    /// </summary>
    public async Task<bool> UserNameExistsAsync(string userName, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(u => u.UserName == userName);
        
        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether an email address is already in use, optionally excluding a specific user
    /// (when a user is updating their email).
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(u => u.Email == email);

        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated list of users with their threads and comments eagerly loaded.
    /// Ordered by username ascending.
    /// </summary>
    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments)
            .OrderBy(u => u.UserName);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

