using Forum.Application.DTOs.Thread;
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom thread-specific methods.
/// </summary>
public interface IThreadRepository : IRepository<ThreadEntity>
{
    Task<ThreadEntity?> GetByIdWithDetailsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<ThreadEntity?> GetByIdWithCommentsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ThreadEntity> Items, int TotalCount)> GetPagedAsync(
        ThreadFilterParams filterParams,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThreadEntity>> GetByUserId(string userId, int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThreadEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
}