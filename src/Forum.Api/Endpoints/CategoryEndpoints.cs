using Forum.Api.Extensions;
using Forum.Application.DTOs.Category;
using Forum.Application.Features.Categories.Commands;
using Forum.Application.Features.Categories.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for CRUD operations on forum categories (admin-only for write operations).
/// </summary>
public static class CategoryEndpoints
{
    /// <summary> Registers category endpoints under /api/categories </summary>
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories");


        // GET all categories
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllCategoriesQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // GET a single category by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategoryByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        // Added route name so other endpoints (like POST)
        // can reference this route safely using CreatedAtRoute.
        // This avoids hardcoded URL strings.
        .WithName("GetCategoryById")
        .AllowAnonymous();

        // POST a new category to create it (Admin only)
        group.MapPost("/", async (CreateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CreateCategoryCommand(dto.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/categories/{result.Value!.CategoryId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // PUT to update a category name (Admin only)
        group.MapPut("/{id:int}", async (int id, UpdateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UpdateCategoryCommand(id, dto.Name), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // DELETE a category (Admin only)
        group.MapDelete("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteCategoryCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
