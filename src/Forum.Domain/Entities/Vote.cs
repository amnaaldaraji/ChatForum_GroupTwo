namespace Forum.Domain.Entities;

/// <summary>
/// An upvote (+1) or downvote (-1) cast by a user on a comment.
/// Each user may vote only once per comment (enforced by a unique index).
/// </summary>
public class Vote
{
    public int VoteId { get; set; }          // Primary key
    public int Value { get; set; }           // 1 for upvote, -1 for downvote
    public string UserId { get; set; } = string.Empty; // FK to the user who cast the vote
    public int CommentId { get; set; }       // FK to the comment being voted on
    public User User { get; set; } = null!;  // Navigation property to the voting user
    public Comment Comment { get; set; } = null!; // Navigation property to the voted-on comment
}