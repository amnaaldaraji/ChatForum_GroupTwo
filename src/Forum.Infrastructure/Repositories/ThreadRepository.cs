using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for ThreadEntity.
/// Extending the generic Repository{T}> with thread-specific query methods.
/// </summary>
public class ThreadRepository : Repository<ThreadEntity>, IThreadRepository
{
    /// <summary>
    /// Initializes a new instance of the ThreadRepository class.
    /// </summary>
    public ThreadRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a thread by its ID with the author (User) and Category eagerly loaded (without comments).
    /// </summary>
    public async Task<ThreadEntity?> GetByIdWithDetailsAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.ThreadId == threadId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a thread by its ID with full details: author, category, and all comments.
    /// Soft-deleted comments are included only if they have replies, preserving the reply chain.
    /// </summary>
    public async Task<ThreadEntity?> GetByIdWithCommentsAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .Include(t => t.Comments.Where(c => !c.IsDeleted || c.Replies.Any()))
                .ThenInclude(c => c.User)
            .Include(t => t.Comments)
                .ThenInclude(c => c.ParentComment)
            .FirstOrDefaultAsync(t => t.ThreadId == threadId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of threads.
    /// </summary>
    public async Task<(IReadOnlyList<ThreadEntity> Items, int TotalCount)> GetPagedAsync(
        ThreadFilterParams filterParams,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .AsQueryable();
        
        if (filterParams.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == filterParams.CategoryId.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(filterParams.AuthorId))
        {
            query = query.Where(t => t.UserId == filterParams.AuthorId);
        }
        
        if (!string.IsNullOrWhiteSpace(filterParams.SearchTerm))
        {
            query = query.Where(t => t.Title.Contains(filterParams.SearchTerm));
        }
        
        if (filterParams.FromDate.HasValue)
        {
            query = query.Where(t => t.TimeCreated >= filterParams.FromDate.Value);
        }
        
        if (filterParams.ToDate.HasValue)
        {
            query = query.Where(t => t.TimeCreated <= filterParams.ToDate.Value);
        }
        
        query = filterParams.SortBy switch
        {
            ThreadSortBy.Oldest => query.OrderBy(t => t.TimeCreated),
            ThreadSortBy.RecentlyUpdated => query.OrderByDescending(t => t.TimeUpdated),
            ThreadSortBy.MostComments => query.OrderByDescending(t => t.Comments.Count),
            _ => query.OrderByDescending(t => t.TimeCreated)
        };
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
            .Take(filterParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves the most recent threads created by a specific user, ordered by creation time.
    /// Includes the Category for each thread.
    /// </summary>
    public async Task<IReadOnlyList<ThreadEntity>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TimeCreated)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all threads belonging to a specific category, ordered by most recently updated.
    /// Includes the thread author.
    /// </summary>
    public async Task<IReadOnlyList<ThreadEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Where(t => t.CategoryId == categoryId)
            .OrderByDescending(t => t.TimeUpdated)
            .ToListAsync(cancellationToken);
    }
}