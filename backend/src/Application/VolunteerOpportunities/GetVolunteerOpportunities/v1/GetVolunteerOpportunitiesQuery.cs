using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesQuery(
    int PageNumber,
    int PageSize,
    string? Search,
    string? City,
    string? Occurrence,
    string? ParticipationType)
    : IQuery<PagedList<VolunteerOpportunitySummary>>;
