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
		EngagementStatus? status = null,
		TimeSlotId? timeSlotId = null,
		IReadOnlyList<Guid>? volunteerIds = null,
		CancellationToken cancellationToken = default);

	ValueTask<PagedList<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		bool upcoming,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// All engagements for the volunteer, unpaginated and not split into
	/// upcoming/past buckets - used by the account data export (#1076), which
	/// needs the complete history in one shot rather than a page of it.
	/// </summary>
	ValueTask<List<EngagementSummary>> GetAllByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checked-in engagements for the volunteer, unpaginated - used by the
	/// printable engagement record (#1096), which needs the complete
	/// attendance history in one shot rather than a page of it.
	/// </summary>
	ValueTask<List<EngagementSummary>> GetCheckedInByVolunteerAsync(
		UserId volunteerId,
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

	/// <summary>
	/// Every engagement for the opportunity, unpaginated, with the live TimeSlot join
	/// (falling back to the engagement's own snapshot, same rule as <see cref="GetByVolunteerAsync"/>)
	/// and <see cref="EngagementSummary.FeedbackRating"/> populated - fields the paginated
	/// organizer list doesn't need but the CSV roster export does.
	/// </summary>
	ValueTask<List<EngagementSummary>> GetForExportAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	ValueTask<EngagementCalendarInfo?> GetCalendarInfoAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default);
}
