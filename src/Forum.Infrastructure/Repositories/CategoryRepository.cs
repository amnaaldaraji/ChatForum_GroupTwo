using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for Category entities.
/// Extending the generic Repository{T} with category-specific query methods.
/// </summary>
public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    /// <summary>
    /// Initializes a new instance of the CategoryRepository class.
    /// </summary>
    public CategoryRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a category by its ID, eagerly loading its associated threads and each thread's author.
    /// </summary>
    public async Task<Category?> GetByIdWithThreadsAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Threads)
                .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all categories with their threads eagerly loaded.
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllWithThreadCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Threads)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether a category with the specified name already exists, optionally
    /// excluding a specific category (used when updating a category's name).
    /// </summary>
    public async Task<bool> NameExistsAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(c => c.Name == name);
        
        if (excludeCategoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId != excludeCategoryId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}