using Api.Common.Endpoints;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

internal static class EndpointTestHelper
{
	public static WebApplication BuildMinimalAppWithAllEndpoints()
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

	public static IReadOnlyList<RouteEndpoint> GetAllRouteEndpoints(WebApplication app) =>
		((IEndpointRouteBuilder)app).DataSources
			.SelectMany(ds => ds.Endpoints)
			.OfType<RouteEndpoint>()
			.ToList();
}
