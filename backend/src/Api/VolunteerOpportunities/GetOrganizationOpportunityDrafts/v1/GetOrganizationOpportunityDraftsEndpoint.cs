using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetOrganizationOpportunityDrafts.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.GetOrganizationOpportunityDrafts.v1;

internal sealed class GetOrganizationOpportunityDraftsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/organizations/{organizationId:guid}/opportunity-drafts", GetOrganizationOpportunityDraftsAsync)
			.WithName("GetOrganizationOpportunityDrafts")
			.Produces<IReadOnlyList<VolunteerOpportunitySummary>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOrganizationOpportunityDraftsAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? UserId.Create(uid).GetValueOrThrow()
			: throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));

		var drafts = await sender.Send(
			new GetOrganizationOpportunityDraftsQuery(organizationId, userId),
			cancellationToken);

		return Results.Ok(drafts);
	}
}
