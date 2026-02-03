namespace Forum.Domain.Entities;

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
