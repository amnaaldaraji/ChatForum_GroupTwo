using Forum.Application.Features.Comments.Commands;
using Forum.Application.Features.Comments.Queries;
using Forum.Application.DTOs.Comment;
using Forum.Api.Extensions;
using MediatR;

namespace Forum.Api.Endpoints;

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/comments").WithTags("Comments");

        // get comment by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetCommentByIdQuery(id), ct);
                return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
            })
            .WithName("GetCommentById");

        // get comments for thread
        group.MapGet("/thread/{threadId:int}", async (int threadId, IMediator mediator, CancellationToken ct, int pageNumber = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetCommentsByThreadQuery(threadId, pageNumber, pageSize), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest();
        });

        // create comment
        group.MapPost("/{threadId:int}", async (int threadId, CreateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // constructor: (string UserId, int ThreadId, string Content, int? ParentCommentId)
            var result = await mediator.Send(new CreateCommentCommand(userId, threadId, dto.Content, dto.ParentCommentId), ct);
            
            return result.IsSuccess 
                ? Results.CreatedAtRoute("GetCommentById", new { id = result.Value.CommentId }, result.Value) 
                : Results.BadRequest();
        }).RequireAuthorization();

        return app;
    }
}