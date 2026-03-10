using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Updates an existing category's name. Only accessible by admins
/// (authorization enforced at the API endpoint level).
/// /// </summary>
public record UpdateCategoryCommand(int CategoryId, string Name) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for UpdateCategoryCommand.
/// Validates input, checks for duplicate names, updates and persists changes.
/// </summary>
public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure<CategoryDto>("Category not found.", ErrorType.NotFound);
        }
        
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CategoryDto>("Category name is required.");
        }
        
        if (await _categoryRepository.NameExistsAsync(request.Name, request.CategoryId, cancellationToken))
        {
            return Result.Failure<CategoryDto>("A category with this name already exists.", ErrorType.Conflict);
        }
        
        category.Name = request.Name.Trim();
        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.ToCategoryDto());
    }
}

