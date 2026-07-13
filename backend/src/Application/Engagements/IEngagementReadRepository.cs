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

	ValueTask<PagedList<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		bool upcoming,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	ValueTask<OpportunityFeedbackSummary> GetFeedbackByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	ValueTask<EngagementCalendarInfo?> GetCalendarInfoAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default);
}
