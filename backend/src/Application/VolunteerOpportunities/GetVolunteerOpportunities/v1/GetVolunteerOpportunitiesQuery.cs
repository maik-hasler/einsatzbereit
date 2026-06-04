using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesQuery(
	int PageNumber,
	int PageSize,
	string? City,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	double? North,
	double? South,
	double? East,
	double? West,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	string[]? Categories,
	string? Tag)
	: IQuery<PagedList<VolunteerOpportunitySummary>>;
