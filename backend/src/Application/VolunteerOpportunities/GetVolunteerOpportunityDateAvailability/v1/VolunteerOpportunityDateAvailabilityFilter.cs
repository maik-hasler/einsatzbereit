namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;

/// <summary>
/// The listing filters (see <c>VolunteerOpportunityFilter</c>) minus paging and minus
/// the date range itself - the calendar asks "which days in this window have anything,
/// given everything else the visitor has already filtered by", so carrying the picked
/// date range in would make every answer agree with itself and mark nothing else.
/// </summary>
/// <param name="UtcOffsetMinutes">
/// The caller's offset from UTC, used to decide which calendar day a time slot falls on.
/// One offset for the whole window: a slot starting within an hour of local midnight on
/// the far side of a DST change inside the requested window can land on the neighbouring
/// day, which is not worth a per-day timezone conversion for a dot on a day cell.
/// </param>
public sealed record VolunteerOpportunityDateAvailabilityFilter(
	DateTimeOffset From,
	DateTimeOffset To,
	int UtcOffsetMinutes,
	string? Occurrence = null,
	string? ParticipationType = null,
	bool? IsRemote = null,
	double? CenterLatitude = null,
	double? CenterLongitude = null,
	double? RadiusKm = null,
	string[]? Categories = null,
	string? Tag = null,
	string? Keyword = null)
{
	public bool HasRadius => CenterLatitude.HasValue && CenterLongitude.HasValue && RadiusKm is > 0;
}
