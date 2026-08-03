using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
		builder.AddMetricsPortListener();

		builder.Services.AddMetrics();
		builder.Services.AddServiceDiscovery();
		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			http.AddStandardResilienceHandler();
			http.AddServiceDiscovery();
		});

		return builder;
	}

	public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
					.AddMeter(OutboxMeterName)
					.AddPrometheusExporter();
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

	private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
		{
			builder.Services.AddOpenTelemetry().UseOtlpExporter();
		}

		return builder;
	}

	public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		builder.Services.AddHealthChecks()
			.AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

		return builder;
	}

	// Metrics:Port (Metrics__Port in docker-compose.yml) is unset everywhere except
	// staging, where it points Kestrel at a second listener that only Prometheus's
	// internal "monitoring" docker network can reach - never the Traefik-routed main
	// port, so /metrics is never publicly exposed (#1341). Optional/unset elsewhere
	// (local Aspire dev already gets live metrics via its own OTLP dashboard).
	private static TBuilder AddMetricsPortListener<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		if (builder is WebApplicationBuilder webBuilder && TryGetMetricsPort(builder.Configuration, out var port))
		{
			// An explicit Kestrel Listen call replaces ASP.NET Core's default URL
			// binding (ASPNETCORE_HTTP_PORTS, set to 8080 by the base container
			// image) instead of adding to it - without re-adding it here, the app
			// ends up listening only on the metrics port, unreachable to both
			// Traefik and the docker-compose healthcheck even though the process
			// itself is healthy. Caused a staging outage the first time this
			// second listener shipped (Metrics:Port was previously never set, so
			// this branch never ran outside staging).
			var mainPort = int.TryParse(builder.Configuration["ASPNETCORE_HTTP_PORTS"], out var configuredPort)
				? configuredPort
				: 8080;

			webBuilder.WebHost.ConfigureKestrel(options =>
			{
				options.ListenAnyIP(mainPort);
				options.ListenAnyIP(port);
			});
		}

		return builder;
	}

	private static bool TryGetMetricsPort(IConfiguration configuration, out int port) =>
		int.TryParse(configuration["Metrics:Port"], out port);

	public static WebApplication MapDefaultEndpoints(this WebApplication app)
	{
		// Exposed in all environments so deployment health checks and live smoke
		// tests have a target.
		//
		// /health  = readiness: critical dependencies (database, Keycloak) must be
		//            reachable. Returns non-200 when a dependency is down so uptime
		//            monitors and the docker-compose healthcheck see the real state.
		// /alive   = liveness: the process is up. Stays 200 regardless of deps.
		app.MapHealthChecks("/health", new HealthCheckOptions
		{
			Predicate = r => r.Tags.Contains("ready")
		});
		app.MapHealthChecks("/alive", new HealthCheckOptions
		{
			Predicate = r => r.Tags.Contains("live")
		});

		if (TryGetMetricsPort(app.Configuration, out var metricsPort))
		{
			// Deliberately not RequireHost($"*:{port}") - that matches the
			// client-supplied Host header, not which physical Kestrel listener
			// accepted the connection, so a request that reaches the
			// Traefik-routed main port (8080) with a forged
			// "Host: anything:8081" header would satisfy it. Connection.LocalPort
			// reflects the actual socket the OS accepted the connection on and
			// can't be spoofed the same way - only a caller on the
			// docker-compose "monitoring" network (Prometheus) can ever hit the
			// AddMetricsPortListener listener directly (#1341).
			app.Use(async (context, next) =>
			{
				if (context.Request.Path == "/metrics" && context.Connection.LocalPort != metricsPort)
				{
					context.Response.StatusCode = StatusCodes.Status404NotFound;
					return;
				}

				await next(context);
			});

			app.MapPrometheusScrapingEndpoint();
		}

		return app;
	}
}
