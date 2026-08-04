using System.Globalization;
using System.Text;
using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.ExportEngagements.v1;

internal sealed class ExportEngagementsQueryHandler(
	IEngagementReadRepository readRepository,
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<ExportEngagementsQuery, EngagementExportFile>
{
	public async ValueTask<EngagementExportFile> Handle(
		ExportEngagementsQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var engagements = await readRepository.GetForExportAsync(request.OpportunityId, cancellationToken);

		var volunteerIds = engagements
			.Where(e => e.VolunteerId is not null)
			.Select(e => e.VolunteerId!.Value)
			.Distinct()
			.ToList();
		var profileMap = await keycloakUserService.GetUserProfilesAsync(volunteerIds, cancellationToken);

		var content = BuildCsv(engagements, profileMap);
		return new EngagementExportFile($"engagements-{request.OpportunityId.Value}.csv", content);
	}

	private static string BuildCsv(
		List<EngagementSummary> engagements,
		IReadOnlyDictionary<Guid, KeycloakUserProfile> profileMap)
	{
		// CRLF line endings explicitly (RFC 4180), not StringBuilder.AppendLine's
		// Environment.NewLine - a CSV built on Linux CI must byte-for-byte match
		// one built on a Windows dev machine, and Excel/other spreadsheet tools
		// expect CRLF regardless of which OS generated the file.
		var sb = new StringBuilder();
		sb.Append("Name,Status,Time Slot,Check-in Status,Feedback Rating\r\n");

		foreach (var e in engagements)
		{
			var row = new[]
			{
				ResolveName(e, profileMap),
				e.Status,
				FormatTimeSlot(e.TimeSlotStartDateTime, e.TimeSlotEndDateTime),
				e.IsCheckedIn ? "Checked in" : "Not checked in",
				e.FeedbackRating?.ToString(CultureInfo.InvariantCulture) ?? "",
			};
			sb.Append(string.Join(",", row.Select(CsvEscape))).Append("\r\n");
		}

		return sb.ToString();
	}

	private static string ResolveName(EngagementSummary e, IReadOnlyDictionary<Guid, KeycloakUserProfile> profileMap)
	{
		if (e.VolunteerId is not Guid volunteerId)
			return "Anonymized volunteer";

		if (!profileMap.TryGetValue(volunteerId, out var profile))
			return $"Volunteer {volunteerId}";

		var name = profile.FirstName is not null || profile.LastName is not null
			? $"{profile.FirstName} {profile.LastName}".Trim()
			: profile.Username;

		return string.IsNullOrWhiteSpace(name) ? $"Volunteer {volunteerId}" : name;
	}

	private static string FormatTimeSlot(DateTimeOffset? start, DateTimeOffset? end)
	{
		if (start is null)
			return "";

		var startText = start.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		if (end is null)
			return $"{startText} UTC";

		var endText = end.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		return $"{startText} - {endText} UTC";
	}

	// RFC 4180: only quote (and escape embedded quotes in) a field that actually
	// needs it - a volunteer's name or comment field could contain a comma,
	// quote, or newline that would otherwise break the CSV's column boundaries.
	private static string CsvEscape(string value)
	{
		if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
			return value;

		return $"\"{value.Replace("\"", "\"\"")}\"";
	}
}
