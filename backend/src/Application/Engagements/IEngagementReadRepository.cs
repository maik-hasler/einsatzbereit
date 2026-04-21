using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements;

public interface IEngagementReadRepository
{
    ValueTask<List<EngagementSummary>> GetByOpportunityAsync(
        VolunteerOpportunityId opportunityId,
        CancellationToken cancellationToken = default);

    ValueTask<List<EngagementSummary>> GetByVolunteerAsync(
        UserId volunteerId,
        CancellationToken cancellationToken = default);
}
