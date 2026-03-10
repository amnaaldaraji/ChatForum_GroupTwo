namespace Forum.Application.Common.Interfaces;

/// <summary>
/// Coordinates the persistence of changes made across multiple repositories in a single database transaction.
/// Lets CQRS handlers call SaveChangesAsync without depending on the Infrastructure layer directly.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}