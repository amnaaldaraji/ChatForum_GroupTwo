using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;

namespace Forum.Application.Common.Interfaces;

/// <summary>
/// Abstraction for authentication operations (register, login, logout and password change).
/// Defined in the application layer so that CQRS handlers invoke authentication logic
/// without depending on the Infrastructure layer directly.
/// </summary>
public interface IAuthService
{
    Task<Result<object>> RegisterAsync(string username, string email, string password);
    Task<Result<AuthResponse>> LoginAsync(string username, string password);
    Task<Result> LogoutAsync();
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}