namespace Infrastructure.BackgroundJobs;

internal sealed class OutboxOptions
{
	public int BatchSize { get; init; } = 20;

	public int PollIntervalSeconds { get; init; } = 5;

	// After this many failed dispatch attempts a message is moved to a terminal
	// dead-letter state (ProcessedOnUtc stamped, Error left populated) instead of
	// being retried forever - see einsatzbereit#1317.
	public int MaxAttempts { get; init; } = 5;

	// How long a claimed-but-not-yet-processed row is treated as still in flight
	// before a later poll (this replica or another) reclaims it - covers the
	// process crashing between claiming a batch and finishing dispatch. Comfortably
	// above the slowest realistic dispatch (a batch of synchronous SMTP sends), so
	// a healthy in-progress batch is never reclaimed out from under itself (#1729).
	public int ClaimTimeoutSeconds { get; init; } = 300;

	// How long a successfully processed row is kept before OutboxRetentionJob
	// prunes it - the table would otherwise grow without bound, since nothing else
	// ever deletes a processed message. A dead-lettered row (ProcessedOnUtc
	// stamped but Error still populated - see MaxAttempts above) is never pruned
	// regardless of age, since it's the only record that something went wrong.
	public int RetentionDays { get; init; } = 30;

	// How often OutboxRetentionJob checks for processed rows past RetentionDays -
	// a low-frequency housekeeping concern, unlike the processor's 5s dispatch poll.
	public int RetentionCheckIntervalHours { get; init; } = 24;
}
