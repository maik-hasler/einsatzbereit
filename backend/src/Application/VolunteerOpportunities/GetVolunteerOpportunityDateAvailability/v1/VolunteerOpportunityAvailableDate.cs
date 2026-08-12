namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;

/// <param name="Date">
/// ISO 8601 calendar date (yyyy-MM-dd) in the caller's UTC offset, deliberately a
/// string rather than a <see cref="DateOnly"/>: NSwag maps a date-formatted property
/// to a JavaScript <c>Date</c>, which parses "2026-08-13" as UTC midnight and would
/// therefore render as the 12th for any caller west of Greenwich - the exact
/// off-by-one-day this endpoint exists to avoid.
/// </param>
/// <param name="OpportunityCount">
/// Distinct published opportunities with at least one time slot starting on this day.
/// </param>
public sealed record VolunteerOpportunityAvailableDate(
	string Date,
	int OpportunityCount);
