namespace Forum.Domain.Entities;

/// <summary>
/// Forum category that groups related threads (e.g. "Formula 1", "Football").
/// </summary>
public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
}
