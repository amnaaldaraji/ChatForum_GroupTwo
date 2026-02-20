using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Data; 

public class ForumDbContext : IdentityDbContext<User>
{
    public ForumDbContext(DbContextOptions<ForumDbContext> options) : base(options){}
    public DbSet<Category> Categories => Set<Category>();

    // (for some reason I have to include full name here)
    public DbSet<Forum.Domain.Entities.Thread> Threads => Set<Forum.Domain.Entities.Thread>();
    public DbSet<Comment> Comments => Set<Comment>();

    public static List<User> _users = new List<User>();

    public bool SaveUser(User user)
    {
        bool isExist = _users.Any(x => x.Email == user.Email);
        if (!isExist)
        {
            _users.Add(user);
            return true;
        }
        return false;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // CATEGORY
        builder.Entity<Category>(e =>
        {
            e.HasKey(x => x.CategoryId);

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

        });

        // THREAD (for some reason I have to include full name here)
        builder.Entity<Forum.Domain.Entities.Thread>(e =>
        {
            e.HasKey(x => x.ThreadId);

            e.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(x => x.TimeCreated)
                .IsRequired();

            e.Property(x => x.TimeUpdated)
                .IsRequired();

            // per user, many threads
            e.HasOne(x => x.User)
                .WithMany(u => u.Threads)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // per category, many threads
            e.HasOne(x => x.Category)
                .WithMany(c => c.Threads)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // COMMENT
        builder.Entity<Comment>(e =>
        {
            // PK
            e.HasKey(x => x.CommentId);

            e.Property(x => x.Content)
                .IsRequired();

            e.Property(x => x.TimeCreated)
                .IsRequired();

            // per user has many comments
            e.HasOne(x => x.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // per thread has many comments
            e.HasOne(x => x.Thread)
                .WithMany(t => t.Comments)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // per parent comment has many replies
            e.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
