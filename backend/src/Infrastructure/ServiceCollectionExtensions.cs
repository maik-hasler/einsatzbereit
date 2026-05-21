using Application.Achievements;
using Application.Achievements.BadgeCatalog;
using Application.Common;
using Application.Common.Email;
using Application.Common.Geocoding;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Application.Organizations;
using Application.VolunteerOpportunities;
using Infrastructure.Achievements;
using Infrastructure.Email;
using Infrastructure.Geocoding;
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
		services.AddScoped<IDomainEventDispatcher, NullDomainEventDispatcher>();
		services.AddScoped<ISaveChangesInterceptor, DomainEventInterceptor>();

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

		services.AddScoped<INotificationReadRepository, NotificationReadRepository>();

		services.AddScoped<IOrganizationDashboardReadRepository, OrganizationDashboardReadRepository>();

		services.AddScoped<IAchievementReadRepository, AchievementReadRepository>();

		services.ConfigureOptions<BadgeCatalogOptionsSetup>();
		services.AddSingleton<IBadgeCatalogService, BadgeCatalogService>();

		services.ConfigureOptions<SmtpOptionsSetup>();
		services.AddScoped<IEmailService, SmtpEmailService>();

		services.ConfigureOptions<GeocodingOptionsSetup>();
		services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(
			(sp, client) =>
			{
				var geocodingOptions = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
				client.BaseAddress = new Uri(geocodingOptions.BaseUrl.TrimEnd('/') + "/");
				client.Timeout = TimeSpan.FromSeconds(geocodingOptions.TimeoutSeconds);
				client.DefaultRequestHeaders.UserAgent.ParseAdd(geocodingOptions.UserAgent);
			});

		services.ConfigureOptions<KeycloakOptionsSetup>();

		services.AddSingleton<KeycloakAdminTokenProvider>();
		services.AddHttpClient(KeycloakAdminTokenProvider.HttpClientName, (sp, client) =>
		{
			var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
			client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
		});

		services.AddHttpClient<IKeycloakOrganizationService, KeycloakOrganizationService>(
			(sp, client) =>
			{
				var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
				client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
			});

		services.AddHttpClient<IKeycloakUserService, KeycloakUserService>(
			(sp, client) =>
			{
				var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
				client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
			});

		return services;
	}
}
