using Forum.Api.Extensions;
using Forum.Application.DTOs.Vote;
using Forum.Application.Features.Votes.Commands;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for casting upvotes/downvotes on comments.
/// </summary>
public static class VoteEndpoints
{
    /// <summary>
    /// Registers vote-related endpoints under /api/comments/{commentId}/votes.
    /// </summary>
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        // POST to cast a vote on a comment
        app.MapPost("/api/comments/{commentId:int}/votes", async (
                int commentId,
                CastVoteDto dto,
                HttpContext httpContext,
                IMediator mediator,
                CancellationToken ct) =>
            {
                // Extract the authenticated user's ID from the JWT/cookie claims
                var userId = httpContext.User.GetUserId();

                // Reject the request if the user identity could not be resolved
                if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

                // Dispatch the vote command through MediatR to the application layer
                var result = await mediator.Send(new CastVoteCommand(userId, commentId, dto.Value), ct);

                // Return 200 OK with the updated vote state, or a structured problem response on failure
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.ToProblemDetails();
            })
            .RequireAuthorization() // Endpoint requires an authenticated user
            .WithTags("Votes");     // Groups the endpoint under "Votes" in Swagger/OpenAPI

        return app;
    }
}