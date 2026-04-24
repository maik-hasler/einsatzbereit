using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities;
using Infrastructure.Keycloak;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Persistence.Options;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        services.ConfigureOptions<ConnectionStringOptionsSetup>();

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetServices<ISaveChangesInterceptor>());

            options.UseNpgsql(
                sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Einsatzbereit,
                mig => mig.MigrationsAssembly("Infrastructure"));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContextInitializer, ApplicationDbContextInitializer>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IVolunteerOpportunityReadRepository, VolunteerOpportunityReadRepository>();

        services.AddScoped<IEngagementReadRepository, EngagementReadRepository>();

        services.ConfigureOptions<KeycloakOptionsSetup>();

        services.AddHttpClient<IKeycloakOrganizationService, KeycloakOrganizationService>(
            (sp, client) =>
            {
                var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
            });

        return services;
    }
}