using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Meta.GetVolunteerOpportunityMeta.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Meta.GetVolunteerOpportunityMeta.v1;

internal sealed class GetVolunteerOpportunityMetaEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/meta/volunteer-opportunities/{opportunityId:guid}", GetVolunteerOpportunityMetaAsync)
			.WithName("GetVolunteerOpportunityMeta")
			.WithTags("Meta")
			.Produces(StatusCodes.Status200OK, contentType: "text/html")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.ShortPublicRead)
			.MapToApiVersion(1);

	private static async Task<IResult> GetVolunteerOpportunityMetaAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		var html = await sender.Send(
			new GetVolunteerOpportunityMetaQuery(opportunityId, baseUrl), cancellationToken);

		return html is null ? Results.NotFound() : Results.Content(html, "text/html; charset=utf-8");
	}
}
