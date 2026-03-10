using System.Linq.Expressions;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Generic base repository that provides standard CRUD operations for any entity type.
/// Concrete repositories inherit from this class and add entity-specific query methods.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// The database context shared across all repositories within a unit of work.
    /// </summary>
    protected readonly ForumDbContext Context;

    /// <summary>
    /// The EF Core DbSet{T} for the entity type.
    /// </summary>
    protected readonly DbSet<T> DbSet;

    /// <summary>
    /// Initializes a new instance of the Repository{T} class.
    /// </summary>
    public Repository(ForumDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    /// <summary>
    /// Retrieves a single entity by its primary key using DbSet{T}.FindAsync.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new[] { id }, cancellationToken);
    }

    /// <summary>
    /// Retrieves all entities of type T from the database.
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all entities matching the specified predicate.
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new entity to the DbSet.
    /// </summary>
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Marks an existing entity as modified.
    /// </summary>
    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    /// <summary>
    /// Marks an entity for removal from the database (hard delete).
    /// </summary>
    public virtual void Delete(T entity)
    {
        DbSet.Remove(entity);
    }

    /// <summary>
    /// Checks whether any entity matching the specified predicate exists in the database.
    /// </summary>
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// Counts the number of entities matching an optional predicate.
    /// If no predicate is provided, counts all entities of type T.
    /// </summary>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate == null)
        {
            return await DbSet.CountAsync(cancellationToken);
        }
        return await DbSet.CountAsync(predicate, cancellationToken);
    }
}

