using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

namespace Application.VolunteerOpportunities;

public interface IVolunteerOpportunityReadRepository
{
    ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
