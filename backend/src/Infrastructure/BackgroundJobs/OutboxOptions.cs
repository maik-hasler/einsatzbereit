namespace Infrastructure.BackgroundJobs;

internal sealed class OutboxOptions
{
	public int BatchSize { get; init; } = 20;

	public int PollIntervalSeconds { get; init; } = 5;
}
