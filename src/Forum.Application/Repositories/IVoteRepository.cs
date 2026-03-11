using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Repository for managing comment votes (upvotes/downvotes).
/// </summary>
public interface IVoteRepository : IRepository<Vote>
{
    // Retrieves the vote a specific user has cast on a specific comment, or null if none exists
    Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default);

    // Returns a dictionary mapping CommentId → net score (sum of all vote values) for the given comments
    Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default);

    // Returns a dictionary mapping CommentId → the current user's vote value (1, -1, or absent) for the given comments
    Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default);
}