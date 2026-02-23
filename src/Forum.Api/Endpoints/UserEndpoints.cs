using Forum.Api.Extensions;
using Forum.Application.DTOs.User;
using Forum.Application.Features.Users.Commands;
using Forum.Application.Features.Users.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        // GET all users (Admin only)
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllUsers(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // GET a user profile by id using MediatR and return it if found
        group.MapGet("/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
        {

            var result = await mediator.Send(new GetUserProfileQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        // Give this endpoint a stable name for its routing.
        // Makes it simpler to reference for later.
        .WithName("GetUserProfileById")
        .AllowAnonymous();

        // PUT to update a user profile, only allowed for the owner or an Admin
        group.MapPut("/{id}", async (string id, UpdateUserProfileDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            // Get the current logged-in user's id
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            // Check if the user has Admin role
            var isAdmin = httpContext.User.IsAdmin();

            // Make sure only the owner or an Admin can update the profile
            if (userId != id && !isAdmin)
            {
                return Results.Forbid();
            }

            var result = await mediator.Send(new UpdateUserProfileCommand(id, dto.UserName, dto.Email, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE a user account (soft delete), only for the owner or an Admin
        group.MapDelete("/{id}", async (string id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            // Get the current logged-in user's id
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            // Check if the user has Admin role
            var isAdmin = httpContext.User.IsAdmin();

            // Only the owner or an Admin can delete the account
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