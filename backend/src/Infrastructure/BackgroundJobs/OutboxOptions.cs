namespace Infrastructure.BackgroundJobs;

internal sealed class OutboxOptions
{
	public int BatchSize { get; init; } = 20;

	public int PollIntervalSeconds { get; init; } = 5;

	public int MaxAttempts { get; init; } = 5;

	public int ClaimTimeoutSeconds { get; init; } = 300;

	public int RetryBackoffBaseSeconds { get; init; } = 300;

	public int RetentionDays { get; init; } = 30;

	public int RetentionCheckIntervalHours { get; init; } = 24;
}
