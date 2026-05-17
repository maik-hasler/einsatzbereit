using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

namespace Application.VolunteerOpportunities;

public interface IVolunteerOpportunityReadRepository
{
	ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
		VolunteerOpportunityFilter filter,
		CancellationToken cancellationToken = default);

	ValueTask<VolunteerOpportunityDetails?> GetDetailsAsync(
		Guid opportunityId,
		CancellationToken cancellationToken = default);

	ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default);
}
