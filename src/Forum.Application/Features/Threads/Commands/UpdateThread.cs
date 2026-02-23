using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Updates an existing thread's metadata (title and/or category).
/// Both Title and CategoryId are optional — only provided fields are applied.
/// Authorization: only the thread owner or an admin can update.
/// </summary>
public record UpdateThreadCommand(int ThreadId, string? Title, int? CategoryId, string UserId, bool IsAdmin) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for UpdateThreadCommand.
/// Validates ownership/admin access, applies partial updates, and persists changes.
/// </summary>
public class UpdateThreadHandler : IRequestHandler<UpdateThreadCommand, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public UpdateThreadHandler(
        IThreadRepository threadRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(UpdateThreadCommand request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithDetailsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<ThreadDetailDto>("Thread not found.", ErrorType.NotFound);
        }
        
        if (thread.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure<ThreadDetailDto>("You are not authorized to update this thread.", ErrorType.Forbidden);
        }
        
        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result.Failure<ThreadDetailDto>("Thread title cannot be empty.");
            }
            thread.Title = request.Title.Trim();
        }
        
        if (request.CategoryId.HasValue)
        {
            if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId.Value, cancellationToken))
            {
                return Result.Failure<ThreadDetailDto>("Category not found.", ErrorType.NotFound);
            }
            thread.CategoryId = request.CategoryId.Value;
        }
        
        thread.TimeUpdated = DateTime.UtcNow;
        _threadRepository.Update(thread);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return await _mediator.Send(new Features.Threads.Queries.GetThreadByIdQuery(request.ThreadId), cancellationToken);
    }
}