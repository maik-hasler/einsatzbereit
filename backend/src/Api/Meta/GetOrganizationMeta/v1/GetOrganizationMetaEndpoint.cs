using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Meta.GetOrganizationMeta.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Meta.GetOrganizationMeta.v1;

// AllowAnonymous is deliberate: only reached via frontend/nginx.conf.template's
// bot-User-Agent rewrite for /organizations/{id} (einsatzbereit#1680), which carries no
// Bearer token to attach - same rationale as GetSitemapEndpoint / the sibling
// GetVolunteerOpportunityMetaEndpoint.
internal sealed class GetOrganizationMetaEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/meta/organizations/{organizationId:guid}", GetOrganizationMetaAsync)
			.WithName("GetOrganizationMeta")
			.WithTags("Meta")
			.Produces(StatusCodes.Status200OK, contentType: "text/html")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.ShortPublicRead)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOrganizationMetaAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		var html = await sender.Send(
			new GetOrganizationMetaQuery(organizationId, baseUrl), cancellationToken);

		return html is null ? Results.NotFound() : Results.Content(html, "text/html; charset=utf-8");
	}
}
