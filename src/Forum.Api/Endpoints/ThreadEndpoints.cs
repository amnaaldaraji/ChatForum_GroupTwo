using Forum.Application.DTOs.Thread;
using Forum.Application.Features.Threads.Commands;
using Forum.Application.Features.Threads.Queries;
using Forum.Api.Extensions;
using MediatR;

namespace Forum.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/threads")
            .WithTags("Threads");

        // get paged list of threads
        group.MapGet("/", async ([AsParameters] ThreadFilterParams filter, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadsQuery(filter), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // get thread by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetThreadById")
        .AllowAnonymous();

        // create new thread
        group.MapPost("/", async (CreateThreadDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateThreadCommand(userId, dto.Title, dto.CategoryId, dto.Body), ct);
            return result.IsSuccess
                ? Results.Created($"/api/threads/{result.Value!.ThreadId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // update thread
        group.MapPut("/{id:int}", async (int id, UpdateThreadDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateThreadCommand(id, dto.Title, dto.CategoryId, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // delete thread
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteThreadCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}