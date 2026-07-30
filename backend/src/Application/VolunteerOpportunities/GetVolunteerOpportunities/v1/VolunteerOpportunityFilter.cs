namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record VolunteerOpportunityFilter(
	int PageNumber,
	int PageSize,
	string? City = null,
	string? Occurrence = null,
	string? ParticipationType = null,
	bool? IsRemote = null,
	DateTimeOffset? DateFrom = null,
	DateTimeOffset? DateTo = null,
	double? North = null,
	double? South = null,
	double? East = null,
	double? West = null,
	double? CenterLatitude = null,
	double? CenterLongitude = null,
	double? RadiusKm = null,
	string[]? Categories = null,
	string? Tag = null,
	string? Search = null)
{
	public bool HasBoundingBox => North.HasValue && South.HasValue && East.HasValue && West.HasValue;

	public bool HasRadius => CenterLatitude.HasValue && CenterLongitude.HasValue && RadiusKm is > 0;
}
