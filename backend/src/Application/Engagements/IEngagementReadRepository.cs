using Application.Common.Pagination;
using Domain.Engagements;
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
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		bool upcoming,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Distinct volunteer ids with an active (pending or confirmed) engagement on
	/// the opportunity, filtered at the database level using the existing
	/// (OpportunityId, Status) index - or, when <paramref name="timeSlotId"/> is
	/// given, only those engaged on that specific time slot.
	/// </summary>
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
