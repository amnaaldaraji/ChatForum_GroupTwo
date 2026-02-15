using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Query to retrieve a user's full profile, including their recent threads and comments.
/// </summary>
public record GetUserProfileQuery(string UserId) : IRequest<Result<UserProfileDto>>;

/// <summary>
/// Handles the GetUserProfileQuery by aggregating user information,
/// recent threads, and recent comments into a UserProfileDto.
/// </summary>
public class GetUserProfileHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly ICommentRepository _commentRepository;

    public GetUserProfileHandler(
        IUserRepository userRepository,
        IThreadRepository threadRepository,
        ICommentRepository commentRepository)
    {
        _userRepository = userRepository;
        _threadRepository = threadRepository;
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query to build a full user profile with recent activity.
    /// </summary>
    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure<UserProfileDto>("User not found.");
        }
        
        var recentThreads = await _threadRepository.GetByUserIdAsync(request.UserId, 5, cancellationToken);
        
        var recentComments = await _commentRepository.GetByUserIdAsync(request.UserId, 5, cancellationToken);

        // Map thread and comment entities to their respective DTOs
        var threadDtos = recentThreads.Select(t => t.ToThreadSummaryDto()).ToList();
        var commentDtos = recentComments.Select(c => c.ToCommentDto()).ToList();

        // Step 5: Compose the full profile DTO with user info and recent activity
        return Result.Success(user.ToUserProfileDto(threadDtos, commentDtos));
    }
}

