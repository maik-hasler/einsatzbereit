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
			.Produces(StatusCodes.Status302Found)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOpportunityBannerAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var url = await sender.Send(
			new GetOpportunityBannerQuery(opportunityId),
			cancellationToken);

		return url is null ? Results.NotFound() : Results.Redirect(url);
	}
}
