using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Common.RateLimiting;

public interface ICheckInAttemptLimiter
{
	Task<bool> IsLockedOutAsync(UserId volunteerId, VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);

	Task RegisterFailedAttemptAsync(UserId volunteerId, VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);

	Task ResetAsync(UserId volunteerId, VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);
}
