using Application.Abstractions;
using Infrastructure.Keycloak;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Persistence.Options;
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
                sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Database,
                mig => mig.MigrationsAssembly("DatabaseMigrations"));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContextInitializer, ApplicationDbContextInitializer>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.ConfigureOptions<KeycloakOptionsSetup>();

        services.AddHttpClient<IKeycloakOrganisationService, KeycloakOrganisationService>(
            (sp, client) =>
            {
                var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
            });

        return services;
    }
}