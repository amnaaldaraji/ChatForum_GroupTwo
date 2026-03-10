using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Repository for managing comment votes (upvotes/downvotes).
/// </summary>
public interface IVoteRepository : IRepository<Vote>
{
    Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default);
}