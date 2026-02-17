using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forum.Api.Endpoints;

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        // skapar en grupp för kommentarer med taggen "Comments"
        var group = app.MapGroup("/api/comments").WithTags("Comments");

        // hämtar en specifik kommentar baserat på id
        group.MapGet("/{id:int}", (int id) => Results.Ok(new { commentId = id }))
            .WithName("GetCommentById");

        // hämtar alla kommentarer som tillhör en viss tråd
        group.MapGet("/thread/{threadId:int}", (int threadId) => Results.Ok(new { threadId }))
            .WithName("GetCommentsByThread");

        // skapar en ny kommentar eller ett svar
        group.MapPost("/", (CommentCreateRequest dto) =>
        {
            var created = new { commentId = 1, content = dto.Content, threadId = dto.ThreadId, parentCommentId = dto.ParentCommentId };
            return Results.Created($"/api/comments/{created.commentId}", created);
        }).WithName("CreateComment");

        // uppdaterar innehållet i en kommentar
        group.MapPut("/{id:int}", (int id, CommentUpdateRequest dto) =>
            Results.Ok(new { commentId = id, content = dto.Content }))
            .WithName("UpdateComment");

        // tar bort en kommentar 
        group.MapDelete("/{id:int}", (int id) => Results.NoContent())
            .WithName("DeleteComment");

        return app;
    }
}

public record CommentCreateRequest(int ThreadId, string Content, int? ParentCommentId);
public record CommentUpdateRequest(string Content);