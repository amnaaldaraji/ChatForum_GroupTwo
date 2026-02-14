using Forum.Application.Common.Models;

namespace Forum.Application.Common.Interfaces;

/// <summary>
/// Abstraction for authentication operations (register, login, logout).
/// Defined in the application layer so that CQRS handlers invoke authentication logic,
/// without depending on the Infrastructure layer directly.
/// </summary>
public interface IAuthService
{
    Task<Result<object>> RegisterAsync(string username, string email, string password);
    Task<Result<AuthResonse>> LoginAsync(string username, string password);
    Task<Result> LogoutAsync();
}