using Application.Common.Pagination;
using Application.Common.Sitemap;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities;

public interface IVolunteerOpportunityReadRepository
{
	ValueTask<IReadOnlyList<SitemapEntry>> GetPublishedForSitemapAsync(
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
		VolunteerOpportunityFilter filter,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Which calendar days inside the filter's window have at least one time slot
	/// starting on them, across the same publicly listed opportunities
	/// <see cref="GetPagedSummariesAsync"/> would return for the same filters.
	/// </summary>
	ValueTask<IReadOnlyList<VolunteerOpportunityAvailableDate>> GetDateAvailabilityAsync(
		VolunteerOpportunityDateAvailabilityFilter filter,
		CancellationToken cancellationToken = default);

	ValueTask<VolunteerOpportunityDetails?> GetDetailsAsync(
		Guid opportunityId,
		Guid? requestingUserId = null,
		CancellationToken cancellationToken = default);

	ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus? status = null,
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus status,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> GetCalendarEventsAsync(
		Guid organizationId,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default);
}
