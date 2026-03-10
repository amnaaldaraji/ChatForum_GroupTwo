using Microsoft.Extensions.DependencyInjection;

namespace Forum.Application;

/// <summary>
/// Extension methods for registering Application layer services with the DI container.
/// Called from the API's Program.cs during application startup to wire up all
/// Application-layer dependencies.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Application layer services into the dependency injection container.
    /// Registers MediatR, which scans the assembly to auto-discover all
    /// IRequest/IRequestHandler pairs (CQRS command and query handlers in the Features folder).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}