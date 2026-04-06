using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesQueryHandler(
    IVolunteerOpportunityReadRepository readRepository)
    : IQueryHandler<GetVolunteerOpportunitiesQuery, PagedList<VolunteerOpportunitySummary>>
{
    public async ValueTask<PagedList<VolunteerOpportunitySummary>> Handle(
        GetVolunteerOpportunitiesQuery request,
        CancellationToken cancellationToken = default) =>
            await readRepository.GetPagedSummariesAsync(request.PageNumber, request.PageSize, cancellationToken);
}
