using System.Linq.Expressions;

namespace Forum.Application.Repositories;

/// <summary>
/// Generic repository interface defining standard CRUD operations across all entities.
/// Serves as the base contract that all entity-specific repositories extend.
/// </summary>
/// <typeparam name="T"> Entity type </typeparam>
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
}