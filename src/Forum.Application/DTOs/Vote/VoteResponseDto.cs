namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Returned after a vote is cast, containing the updated score and the user's current vote state.
/// </summary>
public record VoteResponseDto(int CommentId, int NewScore, int UserVote);