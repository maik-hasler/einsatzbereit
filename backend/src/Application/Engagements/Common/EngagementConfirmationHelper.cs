using Application.Achievements.AwardAchievement.v1;
using Application.Common.Messaging;
using Application.Common.Persistence;
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
		string? timezone,
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

		var tz = ResolveTimeZone(timezone);
		var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
		var isoYear = System.Globalization.ISOWeek.GetYear(now);
		var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		var totalConfirmedEngagements = await RecordActivityStreakAndConfirmationAsync(
			dbContext, sender, volunteerId, isoYear, isoWeek, cancellationToken);

		await EvaluateMilestoneAchievementsAsync(sender, volunteerId, totalConfirmedEngagements, cancellationToken);

		return Result.Success(engagement);
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
