using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Reports.ListReports.v1;
using Domain.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Api.Reports.ListReports.v1;

internal sealed class ListReportsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/admin/reports", ListReportsAsync)
			.WithName("ListReports")
			.WithTags("Admin")
			.Produces<PagedList<AdminReportSummary>>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ListReportsAsync(
		[FromQuery] string? status,
		[FromQuery] int pageNumber,
		[FromQuery] int pageSize,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		ReportStatus? parsedStatus = null;
		if (!string.IsNullOrWhiteSpace(status))
		{
			if (!Enum.TryParse<ReportStatus>(status, ignoreCase: true, out var s))
				return Results.Problem(
					"Invalid status. Allowed values: Pending, Resolved, Dismissed.",
					statusCode: StatusCodes.Status400BadRequest);
			parsedStatus = s;
		}

		var result = await sender.Send(new ListReportsQuery(parsedStatus, pageNumber, pageSize), cancellationToken);

		return Results.Ok(result);
	}
}
