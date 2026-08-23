namespace Infrastructure.BackgroundJobs;

internal sealed class NotificationRetentionOptions
{
	public int ReadRetentionDays { get; init; } = 90;

	public int UnreadRetentionDays { get; init; } = 180;

	public int RetentionCheckIntervalHours { get; init; } = 24;
}
