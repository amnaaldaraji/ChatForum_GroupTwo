using Microsoft.AspNetCore.Identity;

namespace Forum.Domain.Entities;

/// <summary>
/// Forum user, extends ASP.NET Core Identity with soft-delete and navigation properties.
/// </summary>
public class User : IdentityUser
{
    public bool IsDeleted { get; set; }
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
