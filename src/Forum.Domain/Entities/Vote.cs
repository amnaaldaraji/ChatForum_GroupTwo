namespace Forum.Domain.Entities;

/// <summary>
/// An upvote (+1) or downvote (-1) cast by a user on a comment.
/// Each user may vote only once per comment (enforced by a unique index).
/// </summary>
public class Vote
{
    public int VoteId { get; set; }
    public int Value { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CommentId { get; set; }
    public User User { get; set; } = null!;
    public Comment Comment { get; set; } = null!;
}