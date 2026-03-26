using System.Reflection;
using Api.Abstractions;
using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

internal static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services)
    {
        var serviceDescriptors = Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();
        
        services.TryAddEnumerable(serviceDescriptors);
        
        return services;
    }

    public static void MapEndpoints(
        this WebApplication app)
    {
        var apiVersionSet = app
            .NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app
            .MapGroup("v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet);
        
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }
    }
}