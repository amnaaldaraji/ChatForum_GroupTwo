using Forum.Application.Features.Comments.Commands;
using Forum.Application.Features.Comments.Queries;
using Forum.Api.Extensions;
using Forum.Application.DTOs.Comment;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for CRUD operations on comments, including per-thread listing with vote scores.
/// </summary>
public static class CommentEndpoints
{
    /// <summary> Registers comment endpoints under /api/comments. </summary>
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/comments")
            .WithTags("Comments");

        // GET paginated comments (filterable by author, thread, date )
        group.MapGet("/", async ([AsParameters] CommentFilterParams filterParams, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentsQuery(filterParams), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetComments")
        .AllowAnonymous();

        // GET comment by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetCommentById")
        .AllowAnonymous();

        // GET paged comments for thread
        // Pass current userId so vote data is included per user — stays AllowAnonymous,
        // userId is simply null for unauthenticated requests
        group.MapGet("/thread/{threadId:int}", async (
            int threadId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct,
            int pageNumber = 1,
            int pageSize = 50) =>
        {
            var currentUserId = httpContext.User.GetUserId();
            var result = await mediator.Send(
                new GetCommentsByThreadQuery(threadId, pageNumber, pageSize, currentUserId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // POST to create a new comment
        group.MapPost("/{threadId:int}", async (int threadId, CreateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateCommentCommand(userId, threadId, dto.Content, dto.ParentCommentId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/comments/{result.Value!.CommentId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // PUT to update a comment
        group.MapPut("/{id:int}", async (int id, UpdateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateCommentCommand(id, dto.Content, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE to remove a comment
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteCommentCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}