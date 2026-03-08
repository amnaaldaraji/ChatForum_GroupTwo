using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Data;

public class ForumDbContext : IdentityDbContext<User>
{
    public ForumDbContext(DbContextOptions<ForumDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();

    // (for some reason I have to include full name here)
    public DbSet<Forum.Domain.Entities.Thread> Threads => Set<Forum.Domain.Entities.Thread>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Vote> Votes => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // CATEGORY
        builder.Entity<Category>(e =>
        {
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // THREAD
        builder.Entity<Forum.Domain.Entities.Thread>(e =>
        {
            e.HasKey(x => x.ThreadId);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.TimeCreated).IsRequired();
            e.Property(x => x.TimeUpdated).IsRequired();

            e.HasOne(x => x.User)
                .WithMany(u => u.Threads)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category)
                .WithMany(c => c.Threads)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // COMMENT
        builder.Entity<Comment>(e =>
        {
            e.HasKey(x => x.CommentId);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.TimeCreated).IsRequired();

            e.HasOne(x => x.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Thread)
                .WithMany(t => t.Comments)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // per parent comment has many replies
            e.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // VOTE
        builder.Entity<Vote>(e =>
        {
            e.HasKey(x => x.VoteId);
            e.Property(x => x.Value).IsRequired();

            e.HasOne(x => x.User)
                .WithMany(u => u.Votes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Comment)
                .WithMany(c => c.Votes)
                .HasForeignKey(x => x.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            // one vote per user per comment
            e.HasIndex(x => new { x.UserId, x.CommentId }).IsUnique();
        });
    }
}