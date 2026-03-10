using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
// Specify full name for Thread to avoid conflict with System.Threading.Thread
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Infrastructure.Data;

/// <summary>
/// Seeds the database with initial roles, users, categories, threads, and comments.
/// Skips seeding if any users already exist to prevent duplicate data.
/// </summary>
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

    // Ensure required role "Admin" exist.
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
        var adminUser = new User
        {
            UserName = "admin",
            Email = "admin@forum.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");

        var regularUser = new User
        {
            UserName = "john_doe",
            Email = "john@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(regularUser, "User123!");

        var user2 = new User
        {
            UserName = "jane_smith",
            Email = "jane@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user2, "User123!");

        return (adminUser, regularUser, user2);
    }

    // Create a set of default forum categories.
    private static async Task<List<Category>> SeedCategoriesAsync(ForumDbContext context)
    {
        // Prepare a list of default categories.
        var categories = new List<Category>
        {
            new() { Name = "Formula 1" },
            new() { Name = "Football" },
            new() { Name = "Basketball" },
            new() { Name = "Tennis" },
            new() { Name = "Hockey" },
            new() { Name = "Golf" },
            new() { Name = "Cycling" },
            new() { Name = "Rugby" }
        };

        // Add the categories to the context and save so they receive keys.
        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        return categories;
    }

    // Create sample threads associated with seeded users and categories.
    private static async Task<List<ThreadEntity>> SeedThreadsAsync
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
            // Formula 1
            new()
            {
                Title = "2025 Season Predictions - Who takes the championship?",
                UserId = regularUser.Id,
                CategoryId = categories[0].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-10),
                TimeUpdated = DateTime.UtcNow.AddDays(-10)
            },
            // Football
            new()
            {
                Title = "Champions League Semi-Finals Discussion",
                UserId = user2.Id,
                CategoryId = categories[1].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-8),
                TimeUpdated = DateTime.UtcNow.AddDays(-7)
            },
            // Basketball
            new()
            {
                Title = "NBA Playoffs - Who's making it out of the West?",
                UserId = regularUser.Id,
                CategoryId = categories[2].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-6),
                TimeUpdated = DateTime.UtcNow.AddDays(-5)
            },
            // Tennis
            new()
            {
                Title = "Is Sinner the new GOAT?",
                UserId = user2.Id,
                CategoryId = categories[3].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-5),
                TimeUpdated = DateTime.UtcNow.AddDays(-4)
            },
            // Hockey
            new()
            {
                Title = "Stanley Cup contenders this year",
                UserId = adminUser.Id,
                CategoryId = categories[4].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-4),
                TimeUpdated = DateTime.UtcNow.AddDays(-3)
            },
            // Golf
            new()
            {
                Title = "Masters 2025 - Early favorites?",
                UserId = regularUser.Id,
                CategoryId = categories[5].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-3),
                TimeUpdated = DateTime.UtcNow.AddDays(-2)
            },
            // Cycling
            new()
            {
                Title = "Tour de France route looks insane this year",
                UserId = user2.Id,
                CategoryId = categories[6].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-2),
                TimeUpdated = DateTime.UtcNow.AddDays(-1)
            },
            // Rugby
            new()
            {
                Title = "Six Nations 2025 - Ireland vs France was incredible",
                UserId = adminUser.Id,
                CategoryId = categories[7].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-1),
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
            // Thread 0: F1 - body comment
            new()
            {
                Content = "With the new regulations shaking things up, who do you think takes the 2025 Drivers' Championship? I'm leaning towards Verstappen again but Norris is looking seriously quick.",
                UserId = regularUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-10)
            },
            // Thread 0: F1 - reply
            new()
            {
                Content = "McLaren have the best car right now. Norris finally has the machinery to fight for it. My money is on him.",
                UserId = user2.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-9)
            },
            // Thread 0: F1 - reply
            new()
            {
                Content = "Never count out Verstappen. He always finds another gear when it matters most.",
                UserId = adminUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-9).AddHours(2)
            },
            // Thread 1: Football - body comment
            new()
            {
                Content = "The Champions League semis are set! Real Madrid vs Arsenal and Barcelona vs Bayern. What are your predictions?",
                UserId = user2.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-8)
            },
            // Thread 1: Football - reply
            new()
            {
                Content = "Arsenal finally have the squad depth to go all the way. Saka has been unreal this season.",
                UserId = regularUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-7)
            },
            // Thread 1: Football - reply
            new()
            {
                Content = "Real Madrid in the Champions League is a different beast. You just can't bet against them at the Bernabeu.",
                UserId = adminUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-7).AddHours(3)
            },
            // Thread 2: Basketball - body comment
            new()
            {
                Content = "The Western Conference is stacked this year. Thunder, Nuggets, Wolves, and the Mavs all look dangerous. Who's your pick to make the Finals?",
                UserId = regularUser.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-6)
            },
            // Thread 2: Basketball - reply
            new()
            {
                Content = "OKC Thunder all the way. SGA is playing at an MVP level and their defense is elite.",
                UserId = user2.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            // Thread 3: Tennis - body comment
            new()
            {
                Content = "Sinner has been dominating the hard courts and looking untouchable. With Djokovic winding down, is Sinner the next GOAT in the making?",
                UserId = user2.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            // Thread 3: Tennis - reply
            new()
            {
                Content = "He's incredible but let's not forget Alcaraz. Those two are going to have an epic rivalry for years to come.",
                UserId = regularUser.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-4)
            },
            // Thread 4: Hockey - body comment
            new()
            {
                Content = "Who are your top Stanley Cup contenders? I think the Panthers are looking to repeat and Edmonton is hungry after last year's final loss.",
                UserId = adminUser.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-4)
            },
            // Thread 4: Hockey - reply
            new()
            {
                Content = "The Rangers have been quietly building something special. Shesterkin is a wall and their offense is clicking.",
                UserId = regularUser.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-3)
            },
            // Thread 5: Golf - body comment
            new()
            {
                Content = "Augusta is right around the corner. Who are your early picks to win the green jacket this year? Scheffler has to be the favorite.",
                UserId = regularUser.Id,
                ThreadId = threads[5].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-3)
            },
            // Thread 5: Golf - reply
            new()
            {
                Content = "Scheffler is the obvious pick but watch out for Rory. He's due for a Masters win and has been playing incredible golf.",
                UserId = user2.Id,
                ThreadId = threads[5].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            // Thread 6: Cycling - body comment
            new()
            {
                Content = "Have you seen the Tour de France route? Multiple mountain stages back to back. This is going to be a war of attrition. Pogacar vs Vingegaard round 4!",
                UserId = user2.Id,
                ThreadId = threads[6].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            // Thread 6: Cycling - reply
            new()
            {
                Content = "Pogacar is on another level right now. After winning the Giro and Tour double last year, he's the clear favorite.",
                UserId = adminUser.Id,
                ThreadId = threads[6].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1)
            },
            // Thread 7: Rugby - body comment
            new()
            {
                Content = "What a match! Ireland vs France in the Six Nations was an absolute classic. Ireland's defense in the last 10 minutes was heroic.",
                UserId = adminUser.Id,
                ThreadId = threads[7].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1)
            },
            // Thread 7: Rugby - reply
            new()
            {
                Content = "France were brilliant in attack but Ireland just know how to win these tight games. Grand Slam contenders for sure.",
                UserId = regularUser.Id,
                ThreadId = threads[7].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1).AddHours(3)
            }
        };
        // Create a reply linked to the second comment to demonstrate parent/child.
        var firstCommentWithReply = comments[1];
        var replyToFirstComment = new Comment
        {
            Content = "Exactly! The McLaren upgrades have been spot on all season.",
            UserId = regularUser.Id,
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