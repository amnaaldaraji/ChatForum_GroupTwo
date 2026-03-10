using Forum.Application.DTOs.Thread;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom thread-specific methods.
/// Using alias (ThreadEntity) to avoid conflict with System.Threading.Thread.
/// </summary>
public interface IThreadRepository : IRepository<ThreadEntity>
{
    Task<ThreadEntity?> GetByIdWithDetailsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<ThreadEntity?> GetByIdWithCommentsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ThreadEntity> Items, int TotalCount)> GetPagedAsync(
        ThreadFilterParams filterParams,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ThreadEntity>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThreadEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
}