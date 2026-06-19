using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.VerifyOrganization.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.VerifyOrganization.v1;

internal sealed class VerifyOrganizationEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/admin/organizations/{organizationId:guid}/verify", VerifyOrganizationAsync)
			.WithName("VerifyOrganization")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> VerifyOrganizationAsync(
		[FromRoute] Guid organizationId,
		[FromBody] VerifyOrganizationRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var command = new VerifyOrganizationCommand(organizationId, request.IsVerified);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
