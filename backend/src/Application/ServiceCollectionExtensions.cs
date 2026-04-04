using System.Reflection;
using Application.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddTransient<ISender, Sender>();

        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly());
        services.AddBehaviorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }

    private static void AddHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var handlerType = typeof(IRequestHandler<,>);

        var handlers = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType)
                .Select(i => new { Service = i, Implementation = t }));

        foreach (var handler in handlers)
        {
            services.AddTransient(handler.Service, handler.Implementation);
        }
    }

    private static void AddBehaviorsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var behaviorType = typeof(IPipelineBehavior<,>);

        var behaviors = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == behaviorType));

        foreach (var behavior in behaviors)
        {
            // Open generic types must be registered as their generic type definitions,
            // not as constructed generic types with unresolved type parameters.
            if (behavior.IsGenericTypeDefinition)
            {
                services.AddTransient(behaviorType, behavior);
            }
            else
            {
                var serviceType = behavior.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == behaviorType);

                services.AddTransient(serviceType, behavior);
            }
        }
    }
}