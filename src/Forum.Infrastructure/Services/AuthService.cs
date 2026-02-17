using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;
using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Forum.Infrastructure.Services;

/// <summary>
/// Implements authentication operations including user registration,
/// login with JWT token generation, and logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the AuthService class.
    /// </summary>
    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    /// <summary>
    /// Registers a new user with the provided credentials using Identity.
    /// </summary>
    public async Task<Result<object>> RegisterAsync(string username, string email, string password)
    {
        var user = new User
        {
            UserName = username,
            Email = email
        };
        
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure<object>(errors);
        }

        return Result.Success<object>(new { Message = "User registered successfully", UserId = user.Id });
    }

    /// <summary>
    /// Authenticates a user by validating their credentials and generates a JWT token on success.
    /// Rejects soft-deleted users.
    /// </summary>
    public async Task<Result<AuthResponse>> LoginAsync(string username, string password)
    {
        var user = await _userManager.FindByNameAsync(username);
        
        if (user == null || user.IsDeleted)
        {
            return Result.Failure<AuthResponse>("Invalid credentials");
        }
        
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);
        if (!result.Succeeded)
        {
            return Result.Failure<AuthResponse>("Invalid credentials");
        }
        
        var token = await GenerateJwtToken(user);

        return Result.Success(new AuthResponse(token, user.Id, user.UserName!, user.Email));
    }

    /// <summary>
    /// Signs the current user out by clearing the Identity session.
    /// </summary>
    public async Task<Result> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return Result.Success();
    }

    /// <summary>
    /// Generates a JWT for the specified user.
    /// </summary>
    private async Task<string> GenerateJwtToken(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration123!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "ForumApi",
            audience: _configuration["Jwt:Audience"] ?? "ForumClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}