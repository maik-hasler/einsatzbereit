using Application.Achievements.AwardAchievement.v1;
using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.ConfirmEngagement.v1;

internal sealed class ConfirmEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IKeycloakOrganizationService keycloakOrgService,
	IEmailService emailService,
	ISender sender)
	: ICommandHandler<ConfirmEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		ConfirmEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{engagement.OpportunityId.Value}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		engagement.Confirm();

		var notification = Notification.Create(
			engagement.VolunteerId,
			NotificationKind.EngagementConfirmed,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		var tz = ResolveTimeZone(request.Timezone);
		var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
		var isoYear = System.Globalization.ISOWeek.GetYear(now);
		var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		await RecordActivityStreakAsync(engagement.VolunteerId, isoYear, isoWeek, cancellationToken);

		// Count from DB (not yet saved) and +1 for this confirmation
		var confirmedCount = await dbContext.CountConfirmedEngagementsForVolunteerAsync(
			engagement.VolunteerId,
			cancellationToken) + 1;

		await EvaluateMilestoneAchievementsAsync(engagement.VolunteerId, confirmedCount, cancellationToken);

		var volunteer = await keycloakUserService.GetUserAsync(engagement.VolunteerId.Value, cancellationToken);

		await emailService.SendAsync(
			volunteer.Email,
			"Your engagement has been confirmed",
			$"Hello {volunteer.FirstName ?? volunteer.Username},\n\n" +
			$"Your application for \"{opportunity.Title}\" has been confirmed.\n\n" +
			$"We look forward to seeing you!\n\nEinsatzbereit",
			cancellationToken);

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

	private async Task RecordActivityStreakAsync(
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

		if (streak.ActivityStreak >= 4)
		{
			await sender.Send(new AwardAchievementCommand(volunteerId, "weekly-hero-4"), cancellationToken);
		}
	}

	private async Task EvaluateMilestoneAchievementsAsync(
		UserId volunteerId,
		int confirmedCount,
		CancellationToken cancellationToken)
	{
		string[] keysToAward = confirmedCount switch
		{
			1 => ["first-step"],
			5 => ["dedicated-5"],
			100 => ["centurion-100"],
			_ => []
		};

		foreach (var key in keysToAward)
		{
			await sender.Send(new AwardAchievementCommand(volunteerId, key), cancellationToken);
		}
	}
}
