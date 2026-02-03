namespace Forum.Domain.Entities;

public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
}
