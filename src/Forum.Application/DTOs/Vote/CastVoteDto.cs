namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Payload for casting or toggling a vote on a comment.
/// </summary>
/// <param name="Value">Vote direction: 1 (upvote) or -1 (downvote).</param>
public record CastVoteDto(int Value);