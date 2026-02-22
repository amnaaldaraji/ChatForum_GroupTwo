using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Forum.Blazor.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace Forum.Blazor.Services;

/// <summary>
/// AuthenticationStateProvider that determines the current user's identity
/// by reading and parsing the JWT token stored in session storage.
/// </summary>
public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorageService _tokenStorage;

    // A ClaimsPrincipal with no identity — represents an unauthenticated (anonymous) user.
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public ApiAuthenticationStateProvider(ITokenStorageService tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// Called by Blazor's auth system to determine the current user.
    /// Reads the JWT from session storage, parses its claims, and checks expiry.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(_anonymous);

        var claims = ParseClaimsFromJwt(token);
        if (claims == null)
        {
            await _tokenStorage.RemoveTokenAsync();
            return new AuthenticationState(_anonymous);
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Immediately updates the authentication state after a successful login.
    /// Triggers re-evaluation of all AuthorizeView components without a page reload.
    /// </summary>
    public void MarkUserAsAuthenticated(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = claims != null
            ? new ClaimsIdentity(claims, "jwt")
            : new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    /// <summary>
    /// Resets the authentication state to anonymous after logout.
    /// Triggers re-evaluation of all AuthorizeView components.
    /// </summary>
    public void MarkUserAsLoggedOut()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    /// <summary>
    /// Parses claims directly from the JWT payload.
    /// </summary>
    private IEnumerable<Claim>? ParseClaimsFromJwt(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Reject expired tokens — treat user as logged out
            if (jwtToken.ValidTo < DateTime.UtcNow)
                return null;

            var claims = new List<Claim>();
            foreach (var claim in jwtToken.Claims)
            {
                var mappedType = claim.Type switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "unique_name" => ClaimTypes.Name,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    _ => claim.Type
                };
                claims.Add(new Claim(mappedType, claim.Value));
            }

            return claims;
        }
        catch
        {
            return null;
        }
    }
}
