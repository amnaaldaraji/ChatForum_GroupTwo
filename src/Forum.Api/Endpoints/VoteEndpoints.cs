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
                var userId = httpContext.User.GetUserId();
                if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

                var result = await mediator.Send(new CastVoteCommand(userId, commentId, dto.Value), ct);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .WithTags("Votes");

        return app;
    }
}