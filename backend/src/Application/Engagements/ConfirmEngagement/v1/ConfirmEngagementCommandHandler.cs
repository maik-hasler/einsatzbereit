using Application.Achievements.AwardAchievement.v1;
using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.ConfirmEngagement.v1;

internal sealed class ConfirmEngagementCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender)
	: ICommandHandler<ConfirmEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		ConfirmEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{engagement.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		engagement.Confirm().ThrowIfFailure();

		var volunteerId = engagement.VolunteerId!.Value;

		var notification = Notification.Create(
			volunteerId,
			NotificationKind.EngagementConfirmed,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		var tz = ResolveTimeZone(request.Timezone);
		var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
		var isoYear = System.Globalization.ISOWeek.GetYear(now);
		var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		var totalConfirmedEngagements = await RecordActivityStreakAndConfirmationAsync(
			volunteerId, isoYear, isoWeek, cancellationToken);

		await EvaluateMilestoneAchievementsAsync(volunteerId, totalConfirmedEngagements, cancellationToken);

		return engagement;
	}

	private static TimeZoneInfo ResolveTimeZone(string? ianaId)
	{
		if (string.IsNullOrWhiteSpace(ianaId))
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		}
	}

	private async Task<int> RecordActivityStreakAndConfirmationAsync(
		UserId volunteerId,
		int isoYear,
		int isoWeek,
		CancellationToken cancellationToken)
	{
		var streak = await dbContext.GetUserStreakAsync(volunteerId, cancellationToken);
		if (streak is null)
		{
			streak = UserStreak.Create(volunteerId);
			await dbContext.UserStreaks.AddAsync(streak, cancellationToken);
		}
		streak.RecordActivity(isoYear, isoWeek);
		streak.RecordConfirmedEngagement();

		if (streak.ActivityStreak >= 4)
		{
			await sender.Send(new AwardAchievementCommand(volunteerId, "weekly-hero-4"), cancellationToken);
		}

		return streak.TotalConfirmedEngagements;
	}

	// Milestones are keyed by a monotonically-increasing lifetime confirmation
	// count (never decremented by later cancellations/deletions elsewhere), and
	// evaluated with >= rather than an exact match, so a threshold can never be
	// skipped over or made permanently unreachable.
	private static readonly (int Threshold, string Key)[] MilestoneThresholds =
	[
		(1, "first-step"),
		(5, "dedicated-5"),
		(100, "centurion-100"),
	];

	private async Task EvaluateMilestoneAchievementsAsync(
		UserId volunteerId,
		int totalConfirmedEngagements,
		CancellationToken cancellationToken)
	{
		foreach (var (threshold, key) in MilestoneThresholds)
		{
			if (totalConfirmedEngagements >= threshold)
			{
				await sender.Send(new AwardAchievementCommand(volunteerId, key), cancellationToken);
			}
		}
	}
}
