namespace Infrastructure.BackgroundJobs;

internal sealed class SearchAlertDigestOptions
{
	public int MaxBatchSize { get; init; } = 500;

	public int PollIntervalHours { get; init; } = 24;
}
