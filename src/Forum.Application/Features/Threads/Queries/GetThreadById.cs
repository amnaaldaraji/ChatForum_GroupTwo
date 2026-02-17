using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a single thread with full details including the body comment
/// and all reply comments. Used for the thread detail page.
/// </summary>
public record GetThreadByIdQuery(int ThreadId) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for GetThreadByIdQuery.
/// Fetches the thread with comments, identifies the body comment (first comment),
/// separates it from replies, and returns a DTO.
/// </summary>
public class GetThreadByIdHandler : IRequestHandler<GetThreadByIdQuery, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICommentRepository _commentRepository;

    public GetThreadByIdHandler(IThreadRepository threadRepository, ICommentRepository commentRepository)
    {
        _threadRepository = threadRepository;
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(GetThreadByIdQuery request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithCommentsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<ThreadDetailDto>("Thread not found.");
        }
        
        var bodyComment = await _commentRepository.GetFirstCommentByThreadIdAsync(request.ThreadId, cancellationToken);

        // Filter out the body comment from the reply list, sort chronologically, and map to DTOs.
        var comments = thread.Comments?
            .Where(c => c.CommentId != bodyComment?.CommentId) 
            .OrderBy(c => c.TimeCreated)                      
            .Select(c => c.ToCommentDto())
            .ToList() ?? new List<DTOs.Comment.CommentDto>();

        // Map to detail DTO with body and comments separated.
        return Result.Success(thread.ToThreadDetailDto(bodyComment, comments));
    }
}