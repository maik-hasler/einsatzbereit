namespace Infrastructure.BackgroundJobs;

internal sealed class OrganizerNotificationDigestOptions
{
	public int MaxBatchSize { get; init; } = 2000;

	public int PollIntervalHours { get; init; } = 4;
}
