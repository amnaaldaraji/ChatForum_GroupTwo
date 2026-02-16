using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Creates a new forum category. Only accessible by admins
/// (authorization enforced at the API endpoint level).
/// </summary>
public record CreateCategoryCommand(string Name) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for CreateCategoryCommand.
/// Validates input, checks for duplicate names, creates the category entity, and persists it to the database.
/// </summary>
public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CategoryDto>("Category name is required.");
        }
        
        if (await _categoryRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            return Result.Failure<CategoryDto>("A category with this name already exists.");
        }
        
        var category = new Category
        {
            Name = request.Name.Trim()
        };
        
        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.ToCategoryDto());
    }
}

