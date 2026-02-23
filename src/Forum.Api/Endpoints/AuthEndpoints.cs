using Forum.Application.Common.Interfaces;
using Forum.Application.DTOs.Auth;
using Forum.Application.Features.Auth.Commands;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Defines the authentication-related minimal API endpoints for user registration, login, and logout.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps all authentication endpoints to the application's endpoint route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // POST /api/auth/register
        // Dispatches a RegisterUserCommand via MediatR to create a new user account.
        group.MapPost("/register", async (RegisterRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RegisterUserCommand(request.Username, request.Email, request.Password), ct);
            if (result.IsSuccess)
                return Results.Ok(result.Value);

            var errors = result.Error!.Split("; ");
            return Results.Problem(
                title: "Bad Request",
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errors"] = errors });
        }).AllowAnonymous();

        // POST /api/auth/login
        // Dispatches a LoginUserCommand via MediatR to validate credentials and generate a JWT.
        group.MapPost("/login", async (LoginRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new LoginUserCommand(request.Username, request.Password), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(
                    title: "Unauthorized",
                    detail: result.Error,
                    statusCode: StatusCodes.Status401Unauthorized);
        }).AllowAnonymous();

        // POST /api/auth/logout
        // Calls the IAuthService directly to perform server-side logout (sign-out from Identity).
        group.MapPost("/logout", async (IAuthService authService) =>
        {
            var result = await authService.LogoutAsync();
            return Results.Ok(new { Message = "Logged out successfully" });
        }).RequireAuthorization();

        return app;
    }
}

