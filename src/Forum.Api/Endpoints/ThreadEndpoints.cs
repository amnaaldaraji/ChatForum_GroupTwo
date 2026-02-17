using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forum.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        // skapar en grupp för trådar med taggen "Threads"
        var group = app.MapGroup("/api/threads").WithTags("Threads");

        // hämtar en lista med alla trådar
        group.MapGet("/", () => Results.Ok(new { message = "threads list" }))
            .WithName("ListThreads");

        // hämtar en specifik tråd baserat på id
        group.MapGet("/{id:int}", (int id) => Results.Ok(new { threadId = id }))
            .WithName("GetThreadById");

        // skapar en ny tråd
        group.MapPost("/", (HttpContext ctx, ThreadCreateRequest dto) =>
        {
            var created = new { threadId = 1, title = dto.Title, categoryId = dto.CategoryId };
            return Results.Created($"/api/threads/{created.threadId}", created);
        }).WithName("CreateThread");

        // uppdaterar en befintlig tråd
        group.MapPut("/{id:int}", (int id, ThreadUpdateRequest dto) =>
            Results.Ok(new { threadId = id, title = dto.Title, categoryId = dto.CategoryId }))
            .WithName("UpdateThread");

        // tar bort en tråd
        group.MapDelete("/{id:int}", (int id) => Results.NoContent())
            .WithName("DeleteThread");

        return app;
    }
}

public record ThreadCreateRequest(string Title, int CategoryId, string? Body);
public record ThreadUpdateRequest(string Title, int CategoryId);