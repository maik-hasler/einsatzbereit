namespace Infrastructure.Persistence.RateLimiting;

// Persisted counterpart of the old in-memory ConcurrentDictionary-based lockout
// (#1176) - one row per engagement that has ever had a failed check-in PIN
// attempt, so the lockout survives a container restart or a scale-out to
// multiple replicas. CheckInAttemptPruneJob removes rows once their lockout
// window has fully elapsed.
internal sealed class CheckInAttempt
{
	public Guid EngagementId { get; set; }

	public int FailedAttempts { get; set; }

	public DateTimeOffset? LockedUntil { get; set; }

	public DateTimeOffset LastAttemptOn { get; set; }
}
