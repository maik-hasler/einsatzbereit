using Application.Achievements.AwardAchievement.v1;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Time;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.Common;

// Shared by ConfirmEngagementCommandHandler and BulkConfirmEngagementsCommandHandler so the
// latter can confirm each already-loaded, already-authorized engagement in-process instead of
// replaying engagement/opportunity lookups and the ownership check through ISender.Send per item.
internal static class EngagementConfirmationHelper
{
	private static readonly (int Threshold, string Key)[] MilestoneThresholds =
	[
		(1, "first-step"),
		(5, "dedicated-5"),
		(100, "centurion-100"),
	];

	public static async Task<Result<Engagement>> ConfirmAsync(
		IApplicationDbContext dbContext,
		ISender sender,
		Engagement engagement,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var confirmResult = engagement.Confirm();
		if (confirmResult.IsFailure)
			return Result.Failure<Engagement>(confirmResult.Error);

		var volunteerId = engagement.VolunteerId!.Value;

		var notification = Notification.Create(
			volunteerId,
			NotificationKind.EngagementConfirmed,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		// Bucketed by the platform's canonical zone, not whichever organizer happens to
		// confirm the engagement - the volunteer whose ActivityStreak this affects has no
		// say in and no visibility into the confirming organizer's own device zone (#2203).
		var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), CanonicalTimeZone.Value).DateTime;
		var isoYear = System.Globalization.ISOWeek.GetYear(now);
		var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		var totalConfirmedEngagements = await RecordActivityStreakAndConfirmationAsync(
			dbContext, sender, volunteerId, isoYear, isoWeek, cancellationToken);

		await EvaluateMilestoneAchievementsAsync(sender, volunteerId, totalConfirmedEngagements, cancellationToken);

		return Result.Success(engagement);
	}

	private static async Task<int> RecordActivityStreakAndConfirmationAsync(
		IApplicationDbContext dbContext,
		ISender sender,
		UserId volunteerId,
		int isoYear,
		int isoWeek,
		CancellationToken cancellationToken)
	{
		var streak = await dbContext.GetOrCreateUserStreakAsync(volunteerId, cancellationToken);
		streak.RecordActivity(isoYear, isoWeek);
		streak.RecordConfirmedEngagement();

		if (streak.ActivityStreak >= 4)
		{
			await sender.Send(new AwardAchievementCommand(volunteerId, "weekly-hero-4"), cancellationToken);
		}

		return streak.TotalConfirmedEngagements;
	}

	private static async Task EvaluateMilestoneAchievementsAsync(
		ISender sender,
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
