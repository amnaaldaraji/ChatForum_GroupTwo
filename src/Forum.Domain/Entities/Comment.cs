namespace Forum.Domain.Entities;

/// <summary>
/// A comment within a thread. Supports soft-delete, self-referencing replies via ParentCommentId,
/// and upvote/downvote scoring via Votes.
/// </summary>
public class Comment
{
    public int CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime TimeCreated { get; set; }
    public bool IsDeleted { get; set; }

    // Foreign keys
    public string UserId { get; set; } = string.Empty;
    public int ThreadId { get; set; }
    public int? ParentCommentId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Thread Thread { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();

}
