using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetOpportunityBanner.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.VolunteerOpportunities;

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
		OpportunityStatus? status = null,
		CancellationToken cancellationToken = default);

	ValueTask<OpportunityBannerDto?> GetBannerAsync(
		Guid opportunityId,
		CancellationToken cancellationToken = default);
}
