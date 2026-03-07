namespace Forum.Domain.Entities;

public class Vote
{
    public int VoteId { get; set; }
    public int Value { get; set; } // 1 or -1
    public string UserId { get; set; } = string.Empty;
    public int CommentId { get; set; }

    public User User { get; set; } = null!;
    public Comment Comment { get; set; } = null!;
}