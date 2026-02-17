using Forum.Application.DTOs.Category;
using Forum.Application.Features.Categories.Commands;
using Forum.Application.Features.Categories.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories");


        // GET Categories and use MediatR to collect the forum categories, and then Return all the categories
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllCategoriesQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error });
        })
        .AllowAnonymous();

        // GET a single category by id using MediatR and return it if it exists
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategoryByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { result.Error });
        })
        // Added route name so other endpoints (like POST)
        // can reference this route safely using CreatedAtRoute.
        // This avoids hardcoded URL strings.
        .WithName("GetCategoryById")
        .AllowAnonymous();

        // POST a new category and use MediatR to create it (Admin only)
        group.MapPost("/", async (CreateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CreateCategoryCommand(dto.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/categories/{result.Value!.CategoryId}", result.Value)
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // PUT to update a category name using MediatR (Admin only)
        group.MapPut("/{id:int}", async (int id, UpdateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UpdateCategoryCommand(id, dto.Name), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // DELETE a category using MediatR (Admin only)
        group.MapDelete("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteCategoryCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { result.Error });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
