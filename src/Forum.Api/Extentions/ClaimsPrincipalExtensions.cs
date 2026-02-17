using System.Security.Claims;

namespace Forum.Api.Extensions;

/// <summary>
/// Provides extension methods to simplify extraction of user identity information
/// from JWT claims in minimal API endpoints.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's unique identifier from the JWT token claims.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Determines whether the authenticated user has the "Admin" role.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("Admin");
}