using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.DismissInvitation.v1;
using Domain.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.DismissInvitation.v1;

internal sealed class DismissInvitationEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete("/organizations/{organizationId:guid}/invitations/{invitationId:guid}", DismissInvitationAsync)
			.WithName("DismissInvitation")
			.WithTags("Organizations")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> DismissInvitationAsync(
		[FromRoute] Guid organizationId,
		[FromRoute] Guid invitationId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var command = new DismissInvitationCommand(
			new OrganizationId(organizationId),
			new OrganizationInvitationId(invitationId));

		await sender.Send(command, cancellationToken);
		return Results.NoContent();
	}
}
