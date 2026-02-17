using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Queries;

/// <summary>
/// Retrieves all categories with their thread counts.
/// Used by the home page / category listing endpoint.
/// </summary>
public record GetAllCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

/// <summary>
/// Handler for GetAllCategoriesQuery.
/// Fetches all categories from the repository (with thread counts eagerly loaded) and maps them to DTOs.
/// </summary>
public class GetAllCategoriesHandler : IRequestHandler<GetAllCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetAllCategoriesHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query by fetching all categories with thread counts from the database,
    /// mapping each entity to a CategoryDto, and wrapping the result in a success Result.
    /// </summary>
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllWithThreadCountAsync(cancellationToken);

        // Map each Category entity to a CategoryDto using the centralized mapping extensions
        var dtos = categories.Select(c => c.ToCategoryDto()).ToList();

        return Result.Success<IReadOnlyList<CategoryDto>>(dtos);
    }
}