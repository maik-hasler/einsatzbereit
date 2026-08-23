namespace Infrastructure.BackgroundJobs;

internal sealed class AbuseReportRetentionOptions
{
	public int RetentionDaysAfterTargetDeleted { get; init; } = 180;

	public int RetentionCheckIntervalHours { get; init; } = 24;
}
