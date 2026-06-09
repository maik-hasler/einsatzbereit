using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetOpportunityBanner.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetOpportunityBanner.v1;

internal sealed class GetOpportunityBannerEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}/banner", GetOpportunityBannerAsync)
			.WithName("GetOpportunityBanner")
			.Produces(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOpportunityBannerAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var banner = await sender.Send(
			new GetOpportunityBannerQuery(opportunityId),
			cancellationToken);

		if (banner is null)
			return Results.NotFound();

		httpContext.Response.Headers.CacheControl = "public, max-age=300";

		return Results.File(banner.Content, banner.ContentType);
	}
}
