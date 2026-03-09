using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for Comment entities.
/// Extending the generic Repository{T} with comment-specific query methods.
/// </summary>
public class CommentRepository : Repository<Comment>, ICommentRepository
{
    /// <summary>
    /// Initializes a new instance of the CommentRepository class.
    /// </summary>
    public CommentRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a comment by its ID with full details.
    /// </summary>
    public async Task<Comment?> GetByIdWithDetailsAsync(int commentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Include(c => c.Thread)
            .Include(c => c.ParentComment)
                .ThenInclude(pc => pc!.User)
            .FirstOrDefaultAsync(c => c.CommentId == commentId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated list of comments for a specific thread, ordered by creation time ascending.
    /// Soft-deleted comments are included only if they have replies, preserving the reply chain.
    /// </summary>
    public async Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedByThreadIdAsync(
        int threadId,
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default)
    {
        // Find the body comment ID (oldest comment in the thread) so we can exclude it.
        // The first comment serves as the thread body and is displayed separately.
        var bodyCommentId = await DbSet
            .Where(c => c.ThreadId == threadId)
            .OrderBy(c => c.TimeCreated)
            .Select(c => (int?)c.CommentId)
            .FirstOrDefaultAsync(cancellationToken);

        var query = DbSet
            .Include(c => c.User)
            .Include(c => c.ParentComment)
                .ThenInclude(pc => pc!.User)
            .Where(c => c.ThreadId == threadId && (!c.IsDeleted || c.Replies.Any()))
            .Where(c => c.CommentId != bodyCommentId)
            .OrderBy(c => c.TimeCreated);
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a paginated, filtered list of comments across all threads.
    /// Supports filtering by author, thread, and date range. Excludes soft-deleted comments.
    /// </summary>
    public async Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedAsync(
        CommentFilterParams filterParams,
        CancellationToken cancellationToken = default)
    {
        // Exclude thread body comments (the oldest comment in each thread serves as the thread body)
        var bodyCommentIds = DbSet
            .GroupBy(c => c.ThreadId)
            .Select(g => g.OrderBy(c => c.TimeCreated).Select(c => c.CommentId).First());

        var query = DbSet
            .Include(c => c.User)
            .Include(c => c.Thread)
            .Where(c => !c.IsDeleted)
            .Where(c => !bodyCommentIds.Contains(c.CommentId))
            .AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(filterParams.AuthorId))
        {
            query = query.Where(c => c.UserId == filterParams.AuthorId);
        }
        
        if (filterParams.ThreadId.HasValue)
        {
            query = query.Where(c => c.ThreadId == filterParams.ThreadId.Value);
        }
        
        if (filterParams.FromDate.HasValue)
        {
            query = query.Where(c => c.TimeCreated >= filterParams.FromDate.Value);
        }
        
        if (filterParams.ToDate.HasValue)
        {
            query = query.Where(c => c.TimeCreated <= filterParams.ToDate.Value);
        }
        
        query = filterParams.SortBy switch
        {
            CommentSortBy.Oldest => query.OrderBy(c => c.TimeCreated),
            _ => query.OrderByDescending(c => c.TimeCreated)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
            .Take(filterParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves the most recent comments made by a specific user, ordered by creation time descending.
    /// Includes the Thread for each comment.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Thread)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.TimeCreated)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves the oldest comment in a thread, aka the thread body.
    /// Includes the comment's author.
    /// </summary>
    public async Task<Comment?> GetFirstCommentByThreadIdAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Where(c => c.ThreadId == threadId)
            .OrderBy(c => c.TimeCreated)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all direct replies to a specific comment, ordered by creation time ascending.
    /// Includes the author of each reply.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> GetRepliesAsync(int commentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Where(c => c.ParentCommentId == commentId)
            .OrderBy(c => c.TimeCreated)
            .ToListAsync(cancellationToken);
    }
}