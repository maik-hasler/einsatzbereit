using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetOrganizationOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.GetOrganizationOpportunities.v1;

internal sealed class GetOrganizationOpportunitiesEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/organizations/{organizationId:guid}/opportunities", GetOrganizationOpportunitiesAsync)
			.WithName("GetOrganizationOpportunities")
			.Produces<IReadOnlyList<VolunteerOpportunitySummary>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOrganizationOpportunitiesAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? UserId.Create(uid).GetValueOrThrow()
			: throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));

		var opportunities = await sender.Send(
			new GetOrganizationOpportunitiesQuery(organizationId, userId),
			cancellationToken);

		return Results.Ok(opportunities);
	}
}
