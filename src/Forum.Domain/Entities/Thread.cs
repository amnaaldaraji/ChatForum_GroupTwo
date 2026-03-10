namespace Forum.Domain.Entities;

/// <summary>
/// A discussion thread within a category. The thread body is stored as the first comment.
/// </summary>
public class Thread
{
    public int ThreadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    // Foreign keys
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
