namespace Infrastructure.BackgroundJobs;

internal sealed class CheckInAttemptPruneOptions
{
	public int PollIntervalHours { get; init; } = 1;
}
