using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Deletes a category (hard delete). Only accessible by admins.
/// Prevents deletion if the category still contains threads to avoid orphaned data.
/// </summary>
public record DeleteCategoryCommand(int CategoryId) : IRequest<Result>;

/// <summary>
/// Handler for DeleteCategoryCommand.
/// Validates the category exists and has no threads before performing the hard delete.
/// </summary>
public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdWithThreadsAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure("Category not found.", ErrorType.NotFound);
        }
        
        if (category.Threads?.Count > 0)
        {
            return Result.Failure("Cannot delete category that contains threads.", ErrorType.Conflict);
        }
        
        _categoryRepository.Delete(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

