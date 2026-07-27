using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Reports.DismissReport.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Reports.DismissReport.v1;

internal sealed class DismissReportEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/admin/reports/{reportId:guid}/dismiss", DismissReportAsync)
			.WithName("DismissReport")
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

	private static async Task<IResult> DismissReportAsync(
		[FromRoute] Guid reportId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		await sender.Send(new DismissReportCommand(reportId), cancellationToken);

		return Results.NoContent();
	}
}
