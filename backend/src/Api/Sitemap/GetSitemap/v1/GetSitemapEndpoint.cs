using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Sitemap.GetSitemap.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Sitemap.GetSitemap.v1;

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
