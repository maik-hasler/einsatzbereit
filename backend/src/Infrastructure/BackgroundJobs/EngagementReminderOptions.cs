namespace Infrastructure.BackgroundJobs;

internal sealed class EngagementReminderOptions
{
	public int MaxBatchSize { get; init; } = 500;

	public int PollIntervalHours { get; init; } = 1;
}
