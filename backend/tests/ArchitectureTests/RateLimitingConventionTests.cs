using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Asp.Versioning;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

public sealed class RateLimitingConventionTests
{
	[Test]
	public void AllEndpoints_ShouldHaveRateLimitingPolicyApplied()
	{
		var app = BuildMinimalAppWithAllEndpoints();

		var endpointsWithoutRateLimiting = GetAllRouteEndpoints(app)
			.Where(e => GetRateLimitingPolicyName(e) is null)
			.Select(e => e.RoutePattern.RawText)
			.ToList();

		endpointsWithoutRateLimiting.Should().BeEmpty(
			"every endpoint must call RequireRateLimiting() to opt in to a rate limiting policy");
	}

	[Test]
	public void AllEndpoints_ShouldUseOnlyKnownRateLimitingPolicies()
	{
		var knownPolicies = new[] { RateLimitingPolicies.Read, RateLimitingPolicies.Write };

		var app = BuildMinimalAppWithAllEndpoints();

		var endpointsWithUnknownPolicy = GetAllRouteEndpoints(app)
			.Select(e => new { Route = e.RoutePattern.RawText, Policy = GetRateLimitingPolicyName(e) })
			.Where(e => e.Policy is not null && !knownPolicies.Contains(e.Policy))
			.Select(e => $"{e.Route} uses unknown policy '{e.Policy}'")
			.ToList();

		endpointsWithUnknownPolicy.Should().BeEmpty(
			$"endpoints may only use the policies '{RateLimitingPolicies.Read}' or '{RateLimitingPolicies.Write}'");
	}

	// Use type-name reflection instead of IRateLimiterMetadata to avoid a hard dependency
	// on Microsoft.AspNetCore.RateLimiting (which is not reliably resolvable from
	// Microsoft.NET.Sdk test projects even with a FrameworkReference).
	private static string? GetRateLimitingPolicyName(RouteEndpoint endpoint)
	{
		var attr = endpoint.Metadata
			.FirstOrDefault(m => m.GetType().Name == "EnableRateLimitingAttribute");

		return attr?.GetType().GetProperty("PolicyName")?.GetValue(attr) as string;
	}

	private static IReadOnlyList<RouteEndpoint> GetAllRouteEndpoints(WebApplication app) =>
		((IEndpointRouteBuilder)app).DataSources
			.SelectMany(ds => ds.Endpoints)
			.OfType<RouteEndpoint>()
			.ToList();

	private static WebApplication BuildMinimalAppWithAllEndpoints()
	{
		var builder = WebApplication.CreateBuilder();

		builder.Services
			.AddApiVersioning(options =>
			{
				options.DefaultApiVersion = new ApiVersion(1);
				options.AssumeDefaultVersionWhenUnspecified = true;
				options.ApiVersionReader = new UrlSegmentApiVersionReader();
			})
			.AddApiExplorer(options => options.GroupNameFormat = "'v'V");

		builder.Services.AddAuthentication();
		builder.Services.AddAuthorization();
		builder.Services.AddRateLimiter(_ => { });

		// AddEndpoints() uses Assembly.GetExecutingAssembly(), which is ArchitectureTests here.
		// Manually register all IEndpoint implementations from the Api assembly instead.
		var endpointTypes = AssemblyAnchors.PresentationLayer.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false }
				&& typeof(IEndpoint).IsAssignableFrom(t));

		foreach (var type in endpointTypes)
			builder.Services.AddTransient(typeof(IEndpoint), type);

		var app = builder.Build();

		var versionSet = app.NewApiVersionSet()
			.HasApiVersion(new ApiVersion(1))
			.ReportApiVersions()
			.Build();

		var group = app.MapGroup("v{version:apiVersion}").WithApiVersionSet(versionSet);

		foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
			endpoint.MapEndpoint(group);

		return app;
	}
}
