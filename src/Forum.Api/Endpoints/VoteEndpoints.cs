using Forum.Api.Extensions;
using Forum.Application.DTOs.Vote;
using Forum.Application.Features.Votes.Commands;
using MediatR;

namespace Forum.Api.Endpoints;

public static class VoteEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/comments/{commentId}/votes
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