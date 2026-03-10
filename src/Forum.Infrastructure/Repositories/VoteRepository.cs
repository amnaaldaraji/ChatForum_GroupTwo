using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IVoteRepository for managing comment votes.
/// </summary>
public class VoteRepository : Repository<Vote>, IVoteRepository
{
    public VoteRepository(ForumDbContext context) : base(context) { }

    /// <summary>
    /// Returns the vote cast by a specific user on a specific comment, or null if no vote exists.
    /// </summary>
    public async Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(v => v.UserId == userId && v.CommentId == commentId, ct);
    }

    /// <summary>
    /// Returns the net vote score for each comment, keyed by CommentId.
    /// </summary>
    public async Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default)
    {
        var ids = commentIds.ToList();
        return await DbSet
            .Where(v => ids.Contains(v.CommentId))
            .GroupBy(v => v.CommentId)
            .Select(g => new { CommentId = g.Key, Score = g.Sum(v => v.Value) })
            .ToDictionaryAsync(x => x.CommentId, x => x.Score, ct);
    }

    /// <summary>
    /// Returns the current user's vote value for each comment, keyed by CommentId.
    /// </summary>
    public async Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default)
    {
        var ids = commentIds.ToList();
        return await DbSet
            .Where(v => v.UserId == userId && ids.Contains(v.CommentId))
            .ToDictionaryAsync(v => v.CommentId, v => v.Value, ct);
    }
}