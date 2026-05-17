using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.ConfirmEngagement.v1;

internal sealed class ConfirmEngagementCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<ConfirmEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		ConfirmEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		engagement.Confirm();

		var notification = Notification.Create(
			engagement.VolunteerId,
			NotificationKind.EngagementConfirmed,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		var now = DateTime.UtcNow;
		var isoYear = System.Globalization.ISOWeek.GetYear(now);
		var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		await RecordActivityStreakAsync(engagement.VolunteerId, isoYear, isoWeek, cancellationToken);

		return engagement;
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
	}
}
