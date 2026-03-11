using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Vote;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Votes.Commands;

/// <summary>
/// Casts, toggles, or switches a user's vote on a comment.
/// If the user has no vote, creates one. If same direction, removes it. If opposite, switches.
/// </summary>
public record CastVoteCommand(string UserId, int CommentId, int Value) : IRequest<Result<VoteResponseDto>>;

/// <summary>
/// Handles the CastVoteCommand by implementing the vote casting business logic.
/// </summary>
public class CastVoteHandler : IRequestHandler<CastVoteCommand, Result<VoteResponseDto>>
{
    private readonly IVoteRepository _voteRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the CastVoteHandler class.
    /// </summary>
    public CastVoteHandler(IVoteRepository voteRepository, ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _voteRepository = voteRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the CastVoteCommand by processing the vote request.
    /// </summary>
    public async Task<Result<VoteResponseDto>> Handle(CastVoteCommand request, CancellationToken cancellationToken)
    {
        if (request.Value != 1 && request.Value != -1)
            return Result.Failure<VoteResponseDto>("Vote value must be 1 or -1.", ErrorType.Validation);
        
        if (!await _commentRepository.ExistsAsync(c => c.CommentId == request.CommentId, cancellationToken))
            return Result.Failure<VoteResponseDto>("Comment not found.", ErrorType.NotFound);
        
        var existing = await _voteRepository.GetByUserAndCommentAsync(request.UserId, request.CommentId, cancellationToken);

        if (existing == null)
        {
            await _voteRepository.AddAsync(new Vote
            {
                UserId = request.UserId,
                CommentId = request.CommentId,
                Value = request.Value
            }, cancellationToken);
        }
        else if (existing.Value == request.Value)
        {
            // Same direction � toggle off
            _voteRepository.Delete(existing);
        }
        else
        {
            // Opposite direction � switch
            existing.Value = request.Value;
            _voteRepository.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var scores = await _voteRepository.GetScoresForCommentsAsync(
            new[] { request.CommentId }, cancellationToken);
        var newScore = scores.GetValueOrDefault(request.CommentId, 0);

        // Determine a user's current vote after the operation
        var userVotes = await _voteRepository.GetUserVotesForCommentsAsync(
            request.UserId, new[] { request.CommentId }, cancellationToken);
        var userVote = userVotes.GetValueOrDefault(request.CommentId, 0);

        return Result.Success(new VoteResponseDto(request.CommentId, newScore, userVote));
    }
}
