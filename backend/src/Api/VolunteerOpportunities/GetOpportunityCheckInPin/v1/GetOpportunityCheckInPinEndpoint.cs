using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetOpportunityCheckInPin.v1;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.GetOpportunityCheckInPin.v1;

internal sealed class GetOpportunityCheckInPinEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}/check-in-pin", GetCheckInPinAsync)
			.WithName("GetOpportunityCheckInPin")
			.WithTags("VolunteerOpportunities")
			.Produces<string>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetCheckInPinAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? new UserId(uid)
			: throw new DomainException("Invalid user.");

		var query = new GetOpportunityCheckInPinQuery(new VolunteerOpportunityId(opportunityId), userId);
		var pin = await sender.Send(query, cancellationToken);
		return pin is null ? Results.NotFound() : Results.Ok(pin);
	}
}
