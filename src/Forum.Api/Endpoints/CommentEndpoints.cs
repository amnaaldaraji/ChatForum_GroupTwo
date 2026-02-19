using Forum.Application.DTOs.Thread;
using Forum.Application.Features.Comments.Commands;
using Forum.Application.Features.Comments.Queries;
using Forum.Api.Extensions;
using Forum.Application.DTOs.Comment;
using MediatR;

namespace Forum.Api.Endpoints;

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/comments")
            .WithTags("Comments");

        // get comment by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { result.Error });
        })
        .WithName("GetCommentById")
        .AllowAnonymous();

        // get paged comments for thread
        group.MapGet("/thread/{threadId:int}", async (int threadId, IMediator mediator, CancellationToken ct, int pageNumber = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetCommentsByThreadQuery(threadId, pageNumber, pageSize), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error });
        })
        .AllowAnonymous();

        // create new comment
        group.MapPost("/{threadId:int}", async (int threadId, CreateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateCommentCommand(userId, threadId, dto.Content, dto.ParentCommentId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/comments/{result.Value!.CommentId}", result.Value)
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization();

        // update comment
        group.MapPut("/{id:int}", async (int id, UpdateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateCommentCommand(id, dto.Content, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization();

        // delete comment
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteCommentCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization();

        return app;
    }
}