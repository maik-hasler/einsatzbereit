using System.Text;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetOpportunityCalendar.v1;

internal sealed class GetOpportunityCalendarEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}/calendar", GetOpportunityCalendarAsync)
			.WithName("GetOpportunityCalendar")
			.Produces(StatusCodes.Status200OK, contentType: "text/calendar")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetOpportunityCalendarAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var opportunity = await sender.Send(
			new GetVolunteerOpportunityDetailsQuery(opportunityId),
			cancellationToken);

		if (opportunity is null)
			return Results.NotFound();

		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var baseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";
		var opportunityUrl = $"{baseUrl}/volunteer-opportunities/{opportunityId}";

		var address = opportunity.IsRemote
			? "Remote"
			: BuildAddress(opportunity.Street, opportunity.HouseNumber, opportunity.ZipCode, opportunity.City);

		var now = DateTimeOffset.UtcNow;
		var sb = new StringBuilder();
		sb.AppendLine("BEGIN:VCALENDAR");
		sb.AppendLine("VERSION:2.0");
		sb.AppendLine("PRODID:-//Einsatzbereit//EN");
		sb.AppendLine("CALSCALE:GREGORIAN");
		sb.AppendLine("METHOD:PUBLISH");

		if (opportunity.TimeSlots.Count > 0)
		{
			foreach (var slot in opportunity.TimeSlots)
			{
				sb.AppendLine("BEGIN:VEVENT");
				sb.AppendLine($"UID:{opportunityId}-{slot.Id}@einsatzbereit");
				sb.AppendLine($"DTSTAMP:{FormatDateTime(now)}");
				sb.AppendLine($"DTSTART:{FormatDateTime(slot.StartDateTime)}");
				sb.AppendLine($"DTEND:{FormatDateTime(slot.EndDateTime)}");
				AppendICalText(sb, "SUMMARY", opportunity.Title);
				if (!string.IsNullOrWhiteSpace(opportunity.Description))
					AppendICalText(sb, "DESCRIPTION", opportunity.Description);
				if (!string.IsNullOrWhiteSpace(address))
					AppendICalText(sb, "LOCATION", address);
				if (!string.IsNullOrWhiteSpace(opportunityUrl))
					sb.AppendLine($"URL:{opportunityUrl}");
				sb.AppendLine("END:VEVENT");
			}
		}
		else
		{
			sb.AppendLine("BEGIN:VEVENT");
			sb.AppendLine($"UID:{opportunityId}@einsatzbereit");
			sb.AppendLine($"DTSTAMP:{FormatDateTime(now)}");
			sb.AppendLine($"DTSTART;VALUE=DATE:{now:yyyyMMdd}");
			AppendICalText(sb, "SUMMARY", opportunity.Title);
			if (!string.IsNullOrWhiteSpace(opportunity.Description))
				AppendICalText(sb, "DESCRIPTION", opportunity.Description);
			if (!string.IsNullOrWhiteSpace(address))
				AppendICalText(sb, "LOCATION", address);
			if (!string.IsNullOrWhiteSpace(opportunityUrl))
				sb.AppendLine($"URL:{opportunityUrl}");
			sb.AppendLine("END:VEVENT");
		}

		sb.AppendLine("END:VCALENDAR");

		var icsContent = sb.ToString();
		var bytes = Encoding.UTF8.GetBytes(icsContent);

		return Results.File(
			bytes,
			contentType: "text/calendar; charset=utf-8",
			fileDownloadName: $"opportunity-{opportunityId}.ics");
	}

	private static string FormatDateTime(DateTimeOffset dt) =>
		dt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

	private static string BuildAddress(
		string? street,
		string? houseNumber,
		string? zipCode,
		string? city)
	{
		var parts = new List<string>();
		var streetLine = string.Concat(
			string.IsNullOrWhiteSpace(street) ? "" : street,
			string.IsNullOrWhiteSpace(houseNumber) ? "" : $" {houseNumber}").Trim();
		if (!string.IsNullOrWhiteSpace(streetLine))
			parts.Add(streetLine);
		var cityLine = string.Concat(
			string.IsNullOrWhiteSpace(zipCode) ? "" : $"{zipCode} ",
			city ?? "").Trim();
		if (!string.IsNullOrWhiteSpace(cityLine))
			parts.Add(cityLine);
		return string.Join(", ", parts);
	}

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
