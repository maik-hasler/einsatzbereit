using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.GetOrgInvitations.v1;
using Domain.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.GetOrgInvitations.v1;

internal sealed class GetOrgInvitationsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/{organizationId:guid}/invitations", GetOrgInvitationsAsync)
			.WithName("GetOrgInvitations")
			.WithTags("Organizations")
			.Produces<List<OrgInvitationDto>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetOrgInvitationsAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var query = new GetOrgInvitationsQuery(new OrganizationId(organizationId));
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
