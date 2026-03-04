using Forum.Application.Common.Models;
using Forum.Blazor.Models;

namespace Forum.Blazor.Interfaces;

public interface IAuthClientService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RegisterAsync(string username, string email, string password);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();

}
