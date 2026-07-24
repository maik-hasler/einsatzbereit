using System.Text;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.GetEngagementCalendar.v1;

internal sealed class GetEngagementCalendarQueryHandler(
	IEngagementReadRepository readRepository)
	: IQueryHandler<GetEngagementCalendarQuery, EngagementCalendarFile?>
{
	public async ValueTask<EngagementCalendarFile?> Handle(
		GetEngagementCalendarQuery request,
		CancellationToken cancellationToken = default)
	{
		var info = await readRepository.GetCalendarInfoAsync(
			EngagementId.Create(request.EngagementId).GetValueOrThrow(), cancellationToken);

		if (info is null)
			return null;

		var content = BuildIcsContent(info, request.BaseUrl);
		return new EngagementCalendarFile($"engagement-{info.EngagementId}.ics", content);
	}

	private static string BuildIcsContent(EngagementCalendarInfo info, string baseUrl)
	{
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

		return sb.ToString();
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
