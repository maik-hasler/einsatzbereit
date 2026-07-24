using Domain.Engagements;

namespace Application.Common.RateLimiting;

public interface ICheckInAttemptLimiter
{
	Task<bool> IsLockedOutAsync(EngagementId engagementId, CancellationToken cancellationToken = default);

	Task RegisterFailedAttemptAsync(EngagementId engagementId, CancellationToken cancellationToken = default);

	Task ResetAsync(EngagementId engagementId, CancellationToken cancellationToken = default);
}
