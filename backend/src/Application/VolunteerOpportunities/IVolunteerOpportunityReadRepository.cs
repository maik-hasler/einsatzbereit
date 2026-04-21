using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

namespace Application.VolunteerOpportunities;

public interface IVolunteerOpportunityReadRepository
{
    ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? city,
        string? occurrence,
        string? participationType,
        CancellationToken cancellationToken = default);

    ValueTask<VolunteerOpportunityDetails?> GetDetailsAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default);
}
