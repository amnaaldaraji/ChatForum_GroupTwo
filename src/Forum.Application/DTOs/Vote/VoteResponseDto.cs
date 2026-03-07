namespace Forum.Application.DTOs.Vote;

public record VoteResponseDto(int CommentId, int NewScore, int UserVote);