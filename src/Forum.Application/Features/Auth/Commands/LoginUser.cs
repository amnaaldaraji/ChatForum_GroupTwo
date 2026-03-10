using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;
using MediatR;

namespace Forum.Application.Features.Auth.Commands;

/// <summary>
/// Command to authenticate a user and obtain a JWT token.
/// Delegates credential validation and token generation via IAuthService.
/// </summary>
public record LoginUserCommand(string Username, string Password) : IRequest<Result<AuthResponse>>;

/// <summary>
/// Handles the LoginUserCommand by delegating to IAuthService
/// for credential verification and JWT token generation. The auth service validates the
/// username/password pair and returns an AuthResponse containing the token on success.
/// </summary>
public class LoginUserHandler : IRequestHandler<LoginUserCommand, Result<AuthResponse>>
{
    private readonly IAuthService _authService;

    public LoginUserHandler(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Handles the login command by delegating to the auth service.
    /// </summary>
    public async Task<Result<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        // Delegate entirely to the auth service, which handles credential verification and JWT generation
        return await _authService.LoginAsync(request.Username, request.Password);
    }
}

