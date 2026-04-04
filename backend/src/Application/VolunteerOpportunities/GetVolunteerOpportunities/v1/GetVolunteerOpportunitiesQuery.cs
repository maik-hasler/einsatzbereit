using Application.Messaging;
using Application.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesQuery(
    int PageNumber,
    int PageSize)
    : IRequest<PagedList<VolunteerOpportunitySummary>>;
