namespace Infrastructure.BackgroundJobs;

internal sealed class AbuseReportRetentionOptions
{
	// How long a report is kept after the account it targets has been deleted
	// (Report.TargetDeletedOn, stamped by DeleteMyAccountCommandHandler) before
	// AbuseReportRetentionJob prunes it - long enough to still be useful as
	// moderation history for a lingering dispute, but not indefinite once the
	// account itself is gone for good (#1725). Matches
	// NotificationRetentionOptions.UnreadRetentionDays.
	public int RetentionDaysAfterTargetDeleted { get; init; } = 180;

	// How often AbuseReportRetentionJob checks for expired rows - a
	// low-frequency housekeeping concern, unlike a real-time processing job.
	public int RetentionCheckIntervalHours { get; init; } = 24;
}
