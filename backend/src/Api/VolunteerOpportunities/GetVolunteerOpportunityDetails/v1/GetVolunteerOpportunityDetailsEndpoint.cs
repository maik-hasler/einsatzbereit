using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

internal sealed class GetVolunteerOpportunityDetailsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}", GetVolunteerOpportunityDetailsAsync)
			.WithName("GetVolunteerOpportunityDetails")
			.Produces<VolunteerOpportunityDetails>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetVolunteerOpportunityDetailsAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var query = new GetVolunteerOpportunityDetailsQuery(opportunityId);
		var result = await sender.Send(query, cancellationToken);
		return result is null ? Results.NotFound() : Results.Ok(result);
	}
}
