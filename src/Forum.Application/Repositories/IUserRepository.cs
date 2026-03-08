using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom user-specific methods.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdWithDetailsAsync(string userId, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<bool> UserNameExistsAsync(string userName, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(UserFilterParams filterParams, CancellationToken cancellationToken = default);
}