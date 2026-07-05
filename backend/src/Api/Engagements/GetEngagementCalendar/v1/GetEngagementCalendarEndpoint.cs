using System.Text;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.GetEngagementCalendarInfo.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.GetEngagementCalendar.v1;

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
		var info = await sender.Send(
			new GetEngagementCalendarInfoQuery(engagementId),
			cancellationToken);

		if (info is null)
			return Results.NotFound();

		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";
		var opportunityUrl = $"{baseUrl}/volunteer-opportunities/{info.OpportunityId}";

		var now = DateTimeOffset.UtcNow;
		var sb = new StringBuilder();
		sb.AppendLine("BEGIN:VCALENDAR");
		sb.AppendLine("VERSION:2.0");
		sb.AppendLine("PRODID:-//Einsatzbereit//EN");
		sb.AppendLine("CALSCALE:GREGORIAN");
		sb.AppendLine("METHOD:PUBLISH");
		sb.AppendLine("BEGIN:VEVENT");
		sb.AppendLine($"UID:{info.EngagementId}@einsatzbereit");
		sb.AppendLine($"DTSTAMP:{FormatDateTime(now)}");
		sb.AppendLine($"DTSTART:{FormatDateTime(info.StartDateTime)}");
		sb.AppendLine($"DTEND:{FormatDateTime(info.EndDateTime)}");
		AppendICalText(sb, "SUMMARY", info.OpportunityTitle);
		if (!string.IsNullOrWhiteSpace(info.OpportunityDescription))
			AppendICalText(sb, "DESCRIPTION", info.OpportunityDescription);
		if (!string.IsNullOrWhiteSpace(info.Location))
			AppendICalText(sb, "LOCATION", info.Location);
		if (!string.IsNullOrWhiteSpace(opportunityUrl))
			sb.AppendLine($"URL:{opportunityUrl}");
		sb.AppendLine("END:VEVENT");
		sb.AppendLine("END:VCALENDAR");

		var icsContent = sb.ToString();
		var bytes = Encoding.UTF8.GetBytes(icsContent);

		return Results.File(
			bytes,
			contentType: "text/calendar; charset=utf-8",
			fileDownloadName: $"engagement-{info.EngagementId}.ics");
	}

	private static string FormatDateTime(DateTimeOffset dt) =>
		dt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

	private static void AppendICalText(StringBuilder sb, string property, string value)
	{
		// Escape special characters per RFC 5545
		var escaped = value
			.Replace("\\", "\\\\")
			.Replace(";", "\\;")
			.Replace(",", "\\,")
			.Replace("\r\n", "\\n")
			.Replace("\n", "\\n")
			.Replace("\r", "\\n");

		// Fold lines longer than 75 octets (RFC 5545 Section 3.1)
		var line = $"{property}:{escaped}";
		if (line.Length <= 75)
		{
			sb.AppendLine(line);
			return;
		}

		sb.Append(line[..75]);
		var remaining = line[75..];
		while (remaining.Length > 0)
		{
			var chunk = remaining.Length > 74 ? remaining[..74] : remaining;
			sb.Append($"\r\n {chunk}");
			remaining = remaining[chunk.Length..];
		}
		sb.AppendLine();
	}
}
