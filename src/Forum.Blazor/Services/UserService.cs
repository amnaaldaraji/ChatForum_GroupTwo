using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for user profile operations. Calls Forum.Api user endpoints.
/// </summary>
public class UserService : ApiClientBase
{
    public UserService(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/users?pageNumber=...&pageSize=... — returns paginated users (admin only).
    /// </summary>
    public async Task<PagedResult<UserDto>?> GetPagedAsync(int pageNumber = 1, int pageSize = 10)
    {
        return await GetAuthenticatedAsync<PagedResult<UserDto>>(
            $"api/users?pageNumber={pageNumber}&pageSize={pageSize}");
    }

    /// <summary>
    /// GET /api/users/{id} — returns user profile with recent threads and comments.
    /// </summary>
    public async Task<UserProfileDto?> GetProfileAsync(string id)
    {
        return await GetAsync<UserProfileDto>($"api/users/{id}");
    }

    /// <summary>
    /// PUT /api/users/{id} — updates user profile (username, email).
    /// Supports partial updates — only provided fields are changed.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(string id, UpdateUserProfileDto dto)
    {
        return await PutAsync($"api/users/{id}", dto);
    }

    /// <summary>
    /// POST /api/users/{id}/change-password — changes the user's password.
    /// </summary>
    public async Task<HttpResponseMessage> ChangePasswordAsync(string id, string currentPassword, string newPassword)
    {
        return await PostAsync($"api/users/{id}/change-password", new { CurrentPassword = currentPassword, NewPassword = newPassword });
    }

    /// <summary>
    /// DELETE /api/users/{id} — soft-deletes the user account.
    /// The user's display name becomes "Deleted User" across all threads and comments.
    /// </summary>
    public new async Task<HttpResponseMessage> DeleteAsync(string id)
    {
        return await base.DeleteAsync($"api/users/{id}");
    }
}
