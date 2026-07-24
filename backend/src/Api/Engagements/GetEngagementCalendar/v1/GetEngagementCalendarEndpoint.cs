using System.Text;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.GetEngagementCalendar.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.GetEngagementCalendar.v1;

// AllowAnonymous is deliberate: Apple Calendar/webcal subscriptions and
// desktop calendar apps re-fetch this URL directly and cannot attach a
// Bearer token, so the unguessable engagementId (a v7 GUID, never listed
// publicly) acts as a capability token, the same trust model already used
// by the per-opportunity calendar feed this endpoint replaces.
internal sealed class GetEngagementCalendarEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/engagements/{engagementId:guid}/calendar", GetEngagementCalendarAsync)
			.WithName("GetEngagementCalendar")
			.Produces(StatusCodes.Status200OK, contentType: "text/calendar")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetEngagementCalendarAsync(
		[FromRoute] Guid engagementId,
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		var file = await sender.Send(
			new GetEngagementCalendarQuery(engagementId, baseUrl),
			cancellationToken);

		if (file is null)
			return Results.NotFound();

		return Results.File(
			Encoding.UTF8.GetBytes(file.Content),
			contentType: "text/calendar; charset=utf-8",
			fileDownloadName: file.FileName);
	}
}
