namespace Api.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesRequest(
	int PageNumber,
	int PageSize,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	DateTimeOffset? DateFrom,
	DateTimeOffset? DateTo,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	string[]? Categories,
	string? Tag);
