using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using MediatR;

namespace Forum.Application.Features.Auth.Commands;

/// <summary>
/// Command to register a new user account.
/// Delegates the actual registration logic (password hashing, Identity user creation)
/// to IAuthService.
/// </summary>
public record RegisterUserCommand(string Username, string Email, string Password) : IRequest<Result<object>>;

/// <summary>
/// Handles the RegisterUserCommand by delegating to IAuthService
/// for user creation. The auth service is responsible for ASP.NET Core Identity integration,
/// password validation, and any initial role assignment.
/// </summary>
public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, Result<object>>
{
    private readonly IAuthService _authService;

    public RegisterUserHandler(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Handles the registration command by delegating to the auth service.
    /// Forwards the username, email, and password to IAuthService.RegisterAsync
    /// Returns the result (success with user data, or failure with validation errors).
    /// </summary>
    public async Task<Result<object>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Delegate entirely to the auth service, which manages Identity user creation and validation
        return await _authService.RegisterAsync(request.Username, request.Email, request.Password);
    }
}

