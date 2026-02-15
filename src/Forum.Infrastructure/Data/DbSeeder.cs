using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
// Specifies that the ThreadEntity is connected to our defined thread instead of the systems defined thread
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Infrastructure.Data;

public static class DbSeeder
{
    // Entry point that seeds roles, users, categories, threads, and comments.
    public static async Task SeedAsync(ForumDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Skip seeding if any users already exist to avoid duplicate data.
        if (await context.Users.AnyAsync())
        {
            return;
        }

        // Seed roles, then users, categories, threads and comments in that order.
        await SeedRolesAsync(roleManager);
        var (adminUser, regularUser, user2) = await SeedUsersAsync(userManager);
        var categories = await SeedCategoriesAsync(context);
        var threads = await SeedThreadsAsync(context, adminUser, regularUser, user2, categories);
        await SeedCommentsAsync(context, adminUser, regularUser, user2, threads);

        // Persist any remaining changes to the database.
        await context.SaveChangesAsync();
    }

    // Ensure required roles (for example, "Admin") exist.
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
    }

    // Create initial users and assign appropriate roles.
    private static async Task<(User Admin, User Regular, User User2)> SeedUsersAsync(UserManager<User> userManager)
    {
        //
        var adminUser = new User
        {
            UserName = "admin",
            Email = "admin@forum.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");

        //
        var regularUser = new User
        {
            UserName = "john_doe",
            Email = "john@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(regularUser, "User123!");

        //
        var user2 = new User
        {
            UserName = "jane_smith",
            Email = "jane@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user2, "User123!");

        //
        return (adminUser, regularUser, user2);
    }

    // Create a set of default forum categories.
    private static async Task<List<Category>> SeedCategoriesAsync(ForumDbContext context)
    {
        // Prepare a list of default categories.
        var categories = new List<Category>
        {
            new() { Name = "General" },
            new() { Name = "Programming" },
            new() { Name = "News" },
            new() { Name = "Gaming" },
            new() { Name = "Sports" }
        };

        // Add the categories to the context and save so they receive keys.
        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        return categories;
    }

    // Create sample threads associated with seeded users and categories.
    private static async Task<List<ThreadEntity>> SeedThreadsAsync
        //
    (
        ForumDbContext context,
        User adminUser,
        User regularUser,
        User user2,
        List<Category> categories)
    {
        // Sample Thread including their title, id created by and when they were created and updated.
        var threads = new List<ThreadEntity>
        {
            new()
            {
                Title = "Welcome to the Forum! Ask Anything!",
                UserId = adminUser.Id,
                CategoryId = categories[0].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-10),
                TimeUpdated = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Title = "What are the BEST programming languages?",
                UserId = regularUser.Id,
                CategoryId = categories[1].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-8),
                TimeUpdated = DateTime.UtcNow.AddDays(-7)
            },
            new()
            {
                Title = "Best practices for ASP.NET Core",
                UserId = user2.Id,
                CategoryId = categories[1].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-5),
                TimeUpdated = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Title = "Latest AI developments",
                UserId = regularUser.Id,
                CategoryId = categories[2].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-3),
                TimeUpdated = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Title = "What games are recommended?",
                UserId = user2.Id,
                CategoryId = categories[3].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-2),
                TimeUpdated = DateTime.UtcNow.AddDays(-1)
            }
        };
        // Add threads to the context and save to generate keys.
        await context.Threads.AddRangeAsync(threads);
        await context.SaveChangesAsync();

        return threads;
    }

    // Create sample comments and a reply to demonstrate comment threading.
    private static async Task SeedCommentsAsync
        //
    (
        ForumDbContext context,
        User adminUser,
        User regularUser,
        User user2,
        List<ThreadEntity> threads)
    {
        // Prepare a list of sample comments tied to threads and users.
        var comments = new List<Comment>
        {
            new()
            {
                Content = "Welcome everyone! This is a place to discuss anything and everything. Please be respectful and follow the forum rules.",
                UserId = adminUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Content = "Thanks for creating this forum! Looking forward to great discussions.",
                UserId = regularUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-9)
            },
            new()
            {
                Content = "I've been using C# for years and I absolutely love it. The ecosystem is great and it keeps getting better!",
                UserId = regularUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-8)
            },
            new()
            {
                Content = "Python is my go-to for data science and scripting. The simplicity is unmatched.",
                UserId = user2.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-8)
            },
            new()
            {
                Content = "Both are great! I think it really depends on what you're trying to build.",
                UserId = adminUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-7)
            },
            new()
            {
                Content = "Use dependency injection properly, implement the repository pattern, and always use async/await for I/O operations.",
                UserId = user2.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Content = "Don't forget about proper error handling and logging! Serilog is great for logging in ASP.NET Core.",
                UserId = regularUser.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Content = "The recent advances in large language models are incredible. GPT-4 and Claude are game changers.",
                UserId = regularUser.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Content = "I agree! The capabilities are amazing, but we also need to be mindful of the ethical implications.",
                UserId = adminUser.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            // Baldur's gate is peak
            new()
            {
                Content = "Currently playing Baldur's Gate 3. The depth and storytelling are phenomenal!",
                UserId = user2.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Content = "I've been meaning to try that! Right now I'm hooked on Elden Ring.",
                UserId = regularUser.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1)
            }
        };
        // Create a reply linked to the second comment to demonstrate parent/child.
        var firstCommentWithReply = comments[1];
        var replyToFirstComment = new Comment
        {
            Content = "Happy to have you here!",
            UserId = adminUser.Id,
            ThreadId = threads[0].ThreadId,
            ParentCommentId = firstCommentWithReply.CommentId,
            TimeCreated = DateTime.UtcNow.AddDays(-9).AddHours(1)
        };
        // Add initial comments and save so IDs are generated before adding replies.
        await context.Comments.AddRangeAsync(comments);
        await context.SaveChangesAsync();

        replyToFirstComment.ParentCommentId = firstCommentWithReply.CommentId;
        await context.Comments.AddAsync(replyToFirstComment);
        await context.SaveChangesAsync();
    }
}
