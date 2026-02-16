using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Queries;

/// <summary>
/// Retrieves a single category by its ID.
/// Returns a failure Result if the category does not exist.
/// </summary>
public record GetCategoryByIdQuery(int CategoryId) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for GetCategoryByIdQuery.
/// Fetches the category with its threads loaded (for thread count) and maps it to a DTO.
/// </summary>
public class GetCategoryByIdHandler : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryByIdHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query by looking up the category by ID.
    /// Returns a failure Result if not found, otherwise maps the entity to a CategoryDto.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdWithThreadsAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure<CategoryDto>("Category not found.");
        }

        return Result.Success(category.ToCategoryDto());
    }
}