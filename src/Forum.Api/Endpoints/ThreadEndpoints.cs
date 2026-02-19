using Forum.Application.Features.Threads.Commands;
using Forum.Application.Features.Threads.Queries;
using Forum.Application.DTOs.Thread;
using Forum.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forum.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        // create a group for threads with the "Threads" tag
        var group = app.MapGroup("/api/threads").WithTags("Threads");

        // get paged threads using MediatR
        group.MapGet("/", async ([AsParameters] ThreadFilterParams filter, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadsQuery(filter), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Error });
        })
        .WithName("ListThreads")
        .AllowAnonymous();

        // get a specific thread by id using MediatR
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { result.Error });
        })
        .WithName("GetThreadById")
        .AllowAnonymous();

        // create a new thread using MediatR
        group.MapPost("/", async (ThreadCreateRequest dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // constructor: (string UserId, string Title, int CategoryId, string Body)
            var result = await mediator.Send(new CreateThreadCommand(userId, dto.Title, dto.CategoryId, dto.Body), ct);
            
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetThreadById", new { id = result.Value.ThreadId }, result.Value)
                : Results.BadRequest(new { result.Error });
        })
        .WithName("CreateThread")
        .RequireAuthorization();

        // update an existing thread using MediatR
        group.MapPut("/{id:int}", async (int id, ThreadUpdateRequest dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // constructor: (int ThreadId, string? Title, int? CategoryId, string UserId, bool IsAdmin)
            var result = await mediator.Send(new UpdateThreadCommand(id, dto.Title, dto.CategoryId, userId, isAdmin), ct);
            
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Error });
        })
        .WithName("UpdateThread")
        .RequireAuthorization();

        // delete a thread using MediatR
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // constructor: (int ThreadId, string UserId, bool IsAdmin)
            var result = await mediator.Send(new DeleteThreadCommand(id, userId, isAdmin), ct);
            
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(new { result.Error });
        })
        .WithName("DeleteThread")
        .RequireAuthorization();

        return app;
    }
}

public record ThreadCreateRequest(string Title, int CategoryId, string Body);
public record ThreadUpdateRequest(string? Title, int? CategoryId);