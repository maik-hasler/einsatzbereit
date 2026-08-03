using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace Api.AuditLogs.ListAuditLogs.v1;

internal sealed class ListAuditLogsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/admin/audit-logs", ListAuditLogsAsync)
			.WithName("ListAuditLogs")
			.WithTags("Admin")
			.Produces<PagedList<AuditLogEntry>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ListAuditLogsAsync(
		[FromQuery] int pageNumber,
		[FromQuery] int pageSize,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(new ListAuditLogsQuery(pageNumber, pageSize), cancellationToken);

		return Results.Ok(result);
	}
}
