using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Sitemap.GetSitemap.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Sitemap.GetSitemap.v1;

// AllowAnonymous is deliberate: search-engine crawlers fetch this directly, with no
// Bearer token to attach (einsatzbereit#1092). Served under the versioned API prefix
// like every other endpoint - the frontend's nginx proxies its own /sitemap.xml to
// this route over the internal container network, since a sitemap must be served
// from the same host as the URLs it lists while the frontend and backend are
// reached under different hostnames.
internal sealed class GetSitemapEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/sitemap.xml", GetSitemapAsync)
			.WithName("GetSitemap")
			.WithTags("Sitemap")
			.Produces(StatusCodes.Status200OK, contentType: "application/xml")
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.ShortPublicRead)
			.MapToApiVersion(1);

	private static async Task<IResult> GetSitemapAsync(
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		var xml = await sender.Send(new GetSitemapQuery(baseUrl), cancellationToken);

		return Results.Content(xml, "application/xml; charset=utf-8");
	}
}
