using Application.Achievements;
using Application.Achievements.BadgeCatalog;
using Application.Common;
using Application.Common.Email;
using Application.Common.Geocoding;
using Application.Common.Keycloak;
using Application.Common.Maps;
using Application.Common.Persistence;
using Application.Common.RateLimiting;
using Application.Common.Storage;
using Application.Engagements;
using Application.Notifications;
using Application.Organizations;
using Application.Reports;
using Application.VolunteerOpportunities;
using Infrastructure.Achievements;
using Infrastructure.BackgroundJobs;
using Infrastructure.Email;
using Infrastructure.Geocoding;
using Infrastructure.Keycloak;
using Infrastructure.Maps;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Persistence.Options;
using Infrastructure.Persistence.Repositories;
using Infrastructure.RateLimiting;
using Infrastructure.Storage;
using Infrastructure.VolunteerOpportunities;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructureServices(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.ConfigureOptions<ConnectionStringOptionsSetup>();

		services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
		services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
		services.AddScoped<ISaveChangesInterceptor, ConvertDomainEventsToOutboxMessagesInterceptor>();

		services.AddSingleton<IPinGenerator, RandomPinGenerator>();

		services.AddSingleton<ICheckInAttemptLimiter, CheckInAttemptLimiter>();

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

		services.AddScoped<IOrganizationReadRepository, OrganizationReadRepository>();

		services.AddScoped<IEngagementReadRepository, EngagementReadRepository>();

		services.AddScoped<INotificationReadRepository, NotificationReadRepository>();

		services.AddScoped<IOrganizationDashboardReadRepository, OrganizationDashboardReadRepository>();

		services.AddScoped<IAdminOrganizationReadRepository, AdminOrganizationReadRepository>();

		services.AddScoped<IAdminReportReadRepository, AdminReportReadRepository>();

		services.AddScoped<IAchievementReadRepository, AchievementReadRepository>();

		services.ConfigureOptions<BadgeCatalogOptionsSetup>();
		services.AddSingleton<IBadgeCatalogService, BadgeCatalogService>();

		services.ConfigureOptions<SmtpOptionsSetup>();
		services.AddSingleton<EmailMetrics>();
		services.AddScoped<IEmailService, SmtpEmailService>();
		services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
		services.ConfigureOptions<EngagementReminderOptionsSetup>();
		services.AddHostedService<EngagementReminderJob>();
		services.ConfigureOptions<OutboxOptionsSetup>();
		services.AddHostedService<OutboxProcessorJob>();
		services.AddHostedService<GeocodingRetryJob>();
		services.AddHostedService<OrganizationMembershipBackfillJob>();
		services.ConfigureOptions<InvitationExpiryOptionsSetup>();
		services.AddHostedService<InvitationExpiryJob>();


		// IntegrationTests/VisualTests set Geocoding__UseFakeService=true (see
		// AppHost.cs) so they never make a real network call to Nominatim -
		// deterministic, instant, and independent of any HTTP client resilience
		// timing (unlike an earlier version of this override that pointed
		// BaseUrl at an unroutable address instead).
		if (configuration.GetValue<bool>("Geocoding:UseFakeService"))
		{
			services.AddSingleton<IGeocodingService, FakeGeocodingService>();
		}
		else
		{
			services.ConfigureOptions<GeocodingOptionsSetup>();
			services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(
				(sp, client) =>
				{
					var geocodingOptions = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
					client.BaseAddress = new Uri(geocodingOptions.BaseUrl.TrimEnd('/') + "/");
					client.Timeout = TimeSpan.FromSeconds(geocodingOptions.TimeoutSeconds);
					client.DefaultRequestHeaders.UserAgent.ParseAdd(geocodingOptions.UserAgent);
				})
				// ServiceDefaults.AddServiceDefaults applies AddStandardResilienceHandler
				// (3 retries, exponential backoff, ~30s worst case) to every HttpClient by
				// default. Geocoding already has its own retry story - GeocodingHelper's
				// Found/NotFound/TransientFailure classification plus the hourly
				// GeocodingRetryJob - so stacking Polly's retries on top would make a
				// single create/update call block for tens of seconds on any hiccup
				// instead of failing fast to TransientFailure. 1 is the minimum
				// MaxRetryAttempts accepts (0 fails options validation on startup).
				.AddStandardResilienceHandler(options => options.Retry.MaxRetryAttempts = 1);
		}

		services.AddMemoryCache();
		services.ConfigureOptions<MapTileOptionsSetup>();
		services.AddHttpClient<IMapTileService, OpenStreetMapTileService>(
			(sp, client) =>
			{
				var mapTileOptions = sp.GetRequiredService<IOptions<MapTileOptions>>().Value;
				client.BaseAddress = new Uri(mapTileOptions.BaseUrl.TrimEnd('/') + "/");
				client.Timeout = TimeSpan.FromSeconds(mapTileOptions.TimeoutSeconds);
				client.DefaultRequestHeaders.UserAgent.ParseAdd(mapTileOptions.UserAgent);
			});

		services.ConfigureOptions<StorageSettingsSetup>();
		services.AddSingleton<IFileStorageService, MinioFileStorageService>();

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
