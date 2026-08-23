namespace Infrastructure.Persistence.RateLimiting;

internal sealed class CheckInAttempt
{
	public Guid EngagementId { get; set; }

	public int FailedAttempts { get; set; }

	public DateTimeOffset? LockedUntil { get; set; }

	public DateTimeOffset LastAttemptOn { get; set; }
}
