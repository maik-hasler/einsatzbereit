namespace Infrastructure.Persistence.RateLimiting;

internal sealed class CheckInAttempt
{
	public Guid VolunteerId { get; set; }

	public Guid OpportunityId { get; set; }

	public int FailedAttempts { get; set; }

	public DateTimeOffset? LockedUntil { get; set; }

	public DateTimeOffset LastAttemptOn { get; set; }
}
