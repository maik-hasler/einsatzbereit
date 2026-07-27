using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.AdminRestoreOrganization.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.AdminRestoreOrganization.v1;

internal sealed class AdminRestoreOrganizationEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/admin/organizations/{organizationId:guid}/restore", AdminRestoreOrganizationAsync)
			.WithName("AdminRestoreOrganization")
			.WithTags("Admin")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> AdminRestoreOrganizationAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		await sender.Send(new AdminRestoreOrganizationCommand(organizationId), cancellationToken);

		return Results.NoContent();
	}
}
