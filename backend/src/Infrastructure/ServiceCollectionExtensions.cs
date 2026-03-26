using Application.Abstractions;
using Domain.Bedarfe;
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
                sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Database,
                mig => mig.MigrationsAssembly("DatabaseMigrations"));

            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContextInitializer, ApplicationDbContextInitializer>();
        
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IBedarfRepository, BedarfRepository>();
        
        return services;
    }
}