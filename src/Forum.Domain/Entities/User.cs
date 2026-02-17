using Microsoft.AspNetCore.Identity;

namespace Forum.Domain.Entities;

public class User : IdentityUser
{
    public bool IsDeleted { get; set; }
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
