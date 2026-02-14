using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom category-specific methods.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByIdWithThreadsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllWithThreadCountAsync(CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default);
}