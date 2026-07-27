using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;

internal sealed class AdminRestoreVolunteerOpportunityEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/admin/volunteer-opportunities/{opportunityId:guid}/restore", AdminRestoreVolunteerOpportunityAsync)
			.WithName("AdminRestoreVolunteerOpportunity")
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

	private static async Task<IResult> AdminRestoreVolunteerOpportunityAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		await sender.Send(new AdminRestoreVolunteerOpportunityCommand(opportunityId), cancellationToken);

		return Results.NoContent();
	}
}
