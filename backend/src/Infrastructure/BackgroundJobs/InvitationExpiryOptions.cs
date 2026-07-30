namespace Infrastructure.BackgroundJobs;

internal sealed class InvitationExpiryOptions
{
	public int PollIntervalHours { get; init; } = 1;
}
