using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Creates a new thread in a category.
/// The thread body is stored as the first comment (not as a field on Thread).
/// Any authenticated user can create threads.
/// </summary>
public record CreateThreadCommand(string UserId, string Title, int CategoryId, string Body) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for CreateThreadCommand.
/// Validates input, creates both the thread entity and its body comment, persists them, then returns the full thread detail.
/// </summary>
public class CreateThreadHandler : IRequestHandler<CreateThreadCommand, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public CreateThreadHandler(
        IThreadRepository threadRepository,
        ICategoryRepository categoryRepository,
        ICommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(CreateThreadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<ThreadDetailDto>("Thread title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Result.Failure<ThreadDetailDto>("Thread body is required.");
        }
        
        if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId, cancellationToken))
        {
            return Result.Failure<ThreadDetailDto>("Category not found.", ErrorType.NotFound);
        }

        var now = DateTime.UtcNow;
        
        var thread = new ThreadEntity
        {
            Title = request.Title.Trim(),
            CategoryId = request.CategoryId,
            UserId = request.UserId,
            TimeCreated = now,
            TimeUpdated = now
        };
        
        await _threadRepository.AddAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Create the body comment (thread body = first comment)
        var bodyComment = new Comment
        {
            Content = request.Body,
            ThreadId = thread.ThreadId,
            UserId = request.UserId,
            TimeCreated = now
        };

        // Second save: persist the body comment
        await _commentRepository.AddAsync(bodyComment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-query the thread to get the fully populated detail DTO
        // (reuses the GetThreadById query handler)
        return await _mediator.Send(new Queries.GetThreadByIdQuery(thread.ThreadId), cancellationToken);
    }
}