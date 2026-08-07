namespace Infrastructure.BackgroundJobs;

internal sealed class ReportRetentionOptions
{
	// How long a resolved (Dismissed/Actioned) report is kept before
	// ReportRetentionJob prunes it - an Open report is never pruned by this
	// rule alone, since it may still need moderator attention (einsatzbereit#1725).
	public int ResolvedRetentionDays { get; init; } = 180;

	// How often ReportRetentionJob checks for expired/orphaned rows - a
	// low-frequency housekeeping concern, unlike a real-time processing job.
	public int RetentionCheckIntervalHours { get; init; } = 24;
}
