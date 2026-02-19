using Forum.Application.Features.Comments.Commands;
using Forum.Application.Features.Comments.Queries;
using Forum.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forum.Api.Endpoints;

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        // create a group for comments with the "Comments" tag
        var group = app.MapGroup("/api/comments").WithTags("Comments");

        // get a specific comment by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentByIdQuery(id), ct);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.NotFound(new { result.Error });
        })
        .WithName("GetCommentById")
        .AllowAnonymous();

        // get comments for a thread
        group.MapGet("/thread/{threadId:int}", async (int threadId, IMediator mediator, CancellationToken ct, int pageNumber = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetCommentsByThreadQuery(threadId, pageNumber, pageSize), ct);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(new { result.Error });
        })
        .WithName("GetCommentsByThread")
        .AllowAnonymous();

        // create a new comment
        group.MapPost("/", async (CommentCreateRequest dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateCommentCommand(userId, dto.ThreadId, dto.Content, dto.ParentCommentId), ct);
            
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetCommentById", new { id = result.Value.CommentId }, result.Value)
                : Results.BadRequest(new { result.Error });
        })
        .WithName("CreateComment")
        .RequireAuthorization();

        // update comment content
        group.MapPut("/{id:int}", async (int id, CommentUpdateRequest dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateCommentCommand(id, dto.Content, userId, isAdmin), ct);
            
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(new { result.Error });
        })
        .WithName("UpdateComment")
        .RequireAuthorization();

        // delete a comment
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteCommentCommand(id, userId, isAdmin), ct);
            
            return result.IsSuccess 
                ? Results.NoContent() 
                : Results.BadRequest(new { result.Error });
        })
        .WithName("DeleteComment")
        .RequireAuthorization();

        return app;
    }
}

public record CommentCreateRequest(int ThreadId, string Content, int? ParentCommentId);
public record CommentUpdateRequest(string Content);