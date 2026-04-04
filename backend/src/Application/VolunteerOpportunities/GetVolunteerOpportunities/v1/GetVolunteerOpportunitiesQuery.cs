using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesQuery(
    int PageNumber,
    int PageSize)
    : IQuery<PagedList<VolunteerOpportunitySummary>>;
