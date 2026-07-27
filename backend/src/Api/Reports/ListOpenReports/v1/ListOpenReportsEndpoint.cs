using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Reports.ListOpenReports.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Reports.ListOpenReports.v1;

internal sealed class ListOpenReportsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/admin/reports", ListOpenReportsAsync)
			.WithName("ListOpenReports")
			.WithTags("Admin")
			.Produces<PagedList<AdminReportSummary>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ListOpenReportsAsync(
		[FromQuery] int pageNumber,
		[FromQuery] int pageSize,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(new ListOpenReportsQuery(pageNumber, pageSize), cancellationToken);

		return Results.Ok(result);
	}
}
