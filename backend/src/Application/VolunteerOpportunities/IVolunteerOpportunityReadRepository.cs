using Application.Common.Pagination;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
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
		Guid? requestingUserId = null,
		CancellationToken cancellationToken = default);

	ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus? status = null,
		CancellationToken cancellationToken = default);

	ValueTask<string?> GetBannerUrlAsync(
		Guid opportunityId,
		CancellationToken cancellationToken = default);

	ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> GetCalendarEventsAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default);
}
