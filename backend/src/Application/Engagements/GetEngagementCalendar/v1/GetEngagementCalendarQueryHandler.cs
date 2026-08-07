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
		AppendLine(sb, "BEGIN:VCALENDAR");
		AppendLine(sb, "VERSION:2.0");
		AppendLine(sb, "PRODID:-//Einsatzbereit//EN");
		AppendLine(sb, "CALSCALE:GREGORIAN");
		AppendLine(sb, "METHOD:PUBLISH");
		AppendLine(sb, "BEGIN:VEVENT");
		AppendLine(sb, $"UID:{info.EngagementId}@einsatzbereit");
		AppendLine(sb, $"DTSTAMP:{FormatDateTime(now)}");
		AppendLine(sb, $"DTSTART:{FormatDateTime(info.StartDateTime)}");
		AppendLine(sb, $"DTEND:{FormatDateTime(info.EndDateTime)}");
		AppendICalText(sb, "SUMMARY", info.OpportunityTitle);
		if (!string.IsNullOrWhiteSpace(info.OpportunityDescription))
			AppendICalText(sb, "DESCRIPTION", info.OpportunityDescription);
		if (!string.IsNullOrWhiteSpace(info.Location))
			AppendICalText(sb, "LOCATION", info.Location);
		if (!string.IsNullOrWhiteSpace(opportunityUrl))
			AppendLine(sb, $"URL:{opportunityUrl}");
		AppendLine(sb, "END:VEVENT");
		AppendLine(sb, "END:VCALENDAR");

		return sb.ToString();
	}

	private static string FormatDateTime(DateTimeOffset dt) =>
		dt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

	// RFC 5545 requires every line to end in CRLF. StringBuilder.AppendLine
	// uses Environment.NewLine, which is "\n" (not "\r\n") on Linux - where
	// this runs - so the file ended up mixing LF from here with the CRLF
	// AppendICalText's line-folding below already used explicitly (#1729).
	private static void AppendLine(StringBuilder sb, string line) =>
		sb.Append(line).Append("\r\n");

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
			AppendLine(sb, line);
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
		sb.Append("\r\n");
	}
}
