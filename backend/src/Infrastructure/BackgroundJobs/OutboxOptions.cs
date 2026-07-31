namespace Infrastructure.BackgroundJobs;

internal sealed class OutboxOptions
{
	public int BatchSize { get; init; } = 20;

	public int PollIntervalSeconds { get; init; } = 5;

	// After this many failed dispatch attempts a message is moved to a terminal
	// dead-letter state (ProcessedOnUtc stamped, Error left populated) instead of
	// being retried forever - see einsatzbereit#1317.
	public int MaxAttempts { get; init; } = 5;
}
