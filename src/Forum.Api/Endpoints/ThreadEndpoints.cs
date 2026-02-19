using Forum.Application.Features.Threads.Commands;
using Forum.Application.Features.Threads.Queries;
using Forum.Application.DTOs.Thread;
using Forum.Api.Extensions;
using MediatR;

namespace Forum.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/threads").WithTags("Threads");

        // list threads
        group.MapGet("/", async ([AsParameters] ThreadFilterParams filter, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadsQuery(filter), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest();
        });

        // get thread by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        }).WithName("GetThreadById");

        // create thread
        group.MapPost("/", async (CreateThreadDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // constructor: (string UserId, string Title, int CategoryId, string Body)
            var result = await mediator.Send(new CreateThreadCommand(userId, dto.Title, dto.CategoryId, dto.Body), ct);
            
            return result.IsSuccess 
                ? Results.CreatedAtRoute("GetThreadById", new { id = result.Value.ThreadId }, result.Value) 
                : Results.BadRequest();
        }).RequireAuthorization();

        return app;
    }
}