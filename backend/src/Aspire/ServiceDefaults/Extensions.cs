using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class ServiceDefaultsExtensions
{
	// Kept in sync with EmailMetrics.MeterName (Infrastructure/Email/EmailMetrics.cs) -
	// a plain string, not a shared constant, since ServiceDefaults doesn't reference
	// Infrastructure (Aspire's AppHost also depends on this project).
	private const string EmailMeterName = "Einsatzbereit.Email";

	// Kept in sync with OutboxMetrics.MeterName (Infrastructure/BackgroundJobs/OutboxMetrics.cs).
	private const string OutboxMeterName = "Einsatzbereit.Outbox";

	public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		builder.ConfigureOpenTelemetry();
		builder.AddDefaultHealthChecks();

		builder.Services.AddMetrics();
		builder.Services.AddServiceDiscovery();
		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			http.AddStandardResilienceHandler();
			http.AddServiceDiscovery();
		});

		return builder;
	}

	private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		builder.Logging.AddOpenTelemetry(logging =>
		{
			logging.IncludeFormattedMessage = true;
			logging.IncludeScopes = true;
		});

		builder.Services.AddOpenTelemetry()
			.WithMetrics(metrics =>
			{
				metrics
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation()
					.AddRuntimeInstrumentation()
					.AddMeter(EmailMeterName)
					.AddMeter(OutboxMeterName);
			})
			.WithTracing(tracing =>
			{
				tracing
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation();
			});

		builder.AddOpenTelemetryExporters();

		return builder;
	}

	// OTLP is the only export path. Local Aspire dev sets OTEL_EXPORTER_OTLP_ENDPOINT to
	// the dashboard's own collector; anywhere the variable is unset the exporter is never
	// registered and telemetry stays in-process.
	private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
		{
			builder.Services.AddOpenTelemetry().UseOtlpExporter();
		}

		return builder;
	}

	private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		builder.Services.AddHealthChecks()
			.AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

		return builder;
	}

	// configureHealthEndpoint/configureAliveEndpoint let the Api layer (which owns
	// RateLimitingPolicies/OutputCachingPolicies) opt these anonymous, unauthenticated
	// endpoints into its rate limiting and output caching conventions without this
	// shared project taking a reference back onto Api - both are optional, so a
	// caller with no such conventions passes neither and behavior is unchanged (#1172).
	public static WebApplication MapDefaultEndpoints(
		this WebApplication app,
		Action<IEndpointConventionBuilder>? configureHealthEndpoint = null,
		Action<IEndpointConventionBuilder>? configureAliveEndpoint = null)
	{
		var healthEndpoint = app.MapHealthChecks("/health", new HealthCheckOptions
		{
			Predicate = r => r.Tags.Contains("ready")
		});
		var aliveEndpoint = app.MapHealthChecks("/alive", new HealthCheckOptions
		{
			Predicate = r => r.Tags.Contains("live")
		});

		configureHealthEndpoint?.Invoke(healthEndpoint);
		configureAliveEndpoint?.Invoke(aliveEndpoint);

		return app;
	}
}
