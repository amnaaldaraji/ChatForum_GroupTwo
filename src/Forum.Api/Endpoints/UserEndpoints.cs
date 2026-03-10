using Forum.Api.Extensions;
using Forum.Application.DTOs.User;
using Forum.Application.Features.Users.Commands;
using Forum.Application.Features.Users.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for user profiles, password changes, and account management.
/// </summary>
public static class UserEndpoints
{
    /// <summary> Registers user endpoints under /api/users. </summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        // GET paged users (Admin only)
        group.MapGet("/", async (IMediator mediator, CancellationToken ct, int pageNumber = 1, int pageSize = 10, string? sortBy = null) =>
        {
            var sort = Enum.TryParse<Application.DTOs.User.UserSortBy>(sortBy, true, out var parsed)
                ? parsed
                : Application.DTOs.User.UserSortBy.Username;
            var result = await mediator.Send(new GetPagedUsersQuery(pageNumber, pageSize, sort), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // GET a user profile by id
        group.MapGet("/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
        {

            var result = await mediator.Send(new GetUserProfileQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        // Giving this endpoint a stable name for its routing to make it simpler to reference.
        .WithName("GetUserProfileById")
        .AllowAnonymous();

        // PUT to update a user profile, only allowed for the owner or an Admin
        group.MapPut("/{id}", async (string id, UpdateUserProfileDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            
            var isAdmin = httpContext.User.IsAdmin();
            
            if (userId != id && !isAdmin)
            {
                return Results.Forbid();
            }

            var result = await mediator.Send(new UpdateUserProfileCommand(id, dto.UserName, dto.Email, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // POST to change password for a user, only allowed for the owner or an Admin
        group.MapPost("/{id}/change-password", async (string id, ChangePasswordDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var isAdmin = httpContext.User.IsAdmin();
            if (userId != id && !isAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new ChangePasswordCommand(id, dto.CurrentPassword, dto.NewPassword, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(new { Message = "Password changed successfully." })
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE a user account (soft delete), only for the owner or an Admin
        group.MapDelete("/{id}", async (string id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            
            var isAdmin = httpContext.User.IsAdmin();
            
            if (userId != id && !isAdmin)
            {
                return Results.Forbid();
            }

            var result = await mediator.Send(new DeleteUserCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}