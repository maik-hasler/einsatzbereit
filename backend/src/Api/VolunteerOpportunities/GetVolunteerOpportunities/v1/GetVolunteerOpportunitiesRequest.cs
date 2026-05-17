namespace Api.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesRequest(
	int PageNumber,
	int PageSize,
	string? Search,
	string? City,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	DateTimeOffset? DateFrom,
	DateTimeOffset? DateTo,
	double? North,
	double? South,
	double? East,
	double? West,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm);
