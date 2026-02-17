using Forum.Application.Common.Interfaces;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Forum.Infrastructure.Repositories;
using Forum.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Forum.Infrastructure;

/// <summary>
/// Provides extension methods for registering all Infrastructure layer services
/// into the dependency injection container.
/// Includes database context, repositories, Unit of Work, and the authentication service.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure layer services with the dependency injection container.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=forum.db";
        
        services.AddDbContext<ForumDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repository implementations
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IThreadRepository, ThreadRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Register the Unit of Work for coordinating transactional saves across repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register the authentication service
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}