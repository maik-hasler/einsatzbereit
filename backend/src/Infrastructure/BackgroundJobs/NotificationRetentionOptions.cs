namespace Infrastructure.BackgroundJobs;

internal sealed class NotificationRetentionOptions
{
	// How long a read notification is kept before NotificationRetentionJob prunes
	// it - the table would otherwise grow without bound, since nothing else ever
	// deletes a notification for reasons other than the recipient's whole account
	// being deleted. Matches Outbox's ProcessedOnUtc-based retention shape.
	public int ReadRetentionDays { get; init; } = 90;

	// An unread notification can point (via RelatedEntityId) at an entity that
	// has since been deleted, showing a stale/broken link indefinitely until the
	// recipient happens to read it - this longer backstop prunes it regardless of
	// read status once it's old enough that it's no longer actionable (#1209).
	public int UnreadRetentionDays { get; init; } = 180;

	// How often NotificationRetentionJob checks for expired rows - a
	// low-frequency housekeeping concern, unlike a real-time processing job.
	public int RetentionCheckIntervalHours { get; init; } = 24;
}
