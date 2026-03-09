using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

public interface IVoteRepository : IRepository<Vote>
{
    Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default);
}