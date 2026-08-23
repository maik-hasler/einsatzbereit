using Application.Common.Pagination;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements;

public interface IEngagementReadRepository
{
	ValueTask<List<EngagementSummary>> GetByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<EngagementSummary>> GetPagedByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		int pageNumber,
		int pageSize,
		EngagementStatus? status = null,
		TimeSlotId? timeSlotId = null,
		IReadOnlyList<Guid>? volunteerIds = null,
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<EngagementSummary>> GetPagedByOrganizationAsync(
		OrganizationId organizationId,
		int pageNumber,
		int pageSize,
		EngagementStatus? status = null,
		IReadOnlyList<Guid>? volunteerIds = null,
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		bool upcoming,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	ValueTask<List<EngagementSummary>> GetAllByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default);

	ValueTask<List<EngagementSummary>> GetCheckedInByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default);

	ValueTask<List<Guid>> GetActiveVolunteerIdsByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default);

	ValueTask<OpportunityFeedbackSummary> GetFeedbackByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	ValueTask<EngagementCalendarInfo?> GetCalendarInfoAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default);
}
