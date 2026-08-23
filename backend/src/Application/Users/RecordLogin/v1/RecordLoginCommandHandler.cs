using Application.Achievements.AwardAchievement.v1;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.RecordLogin.v1;

internal sealed class RecordLoginCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender)
	: ICommandHandler<RecordLoginCommand, bool>
{
	public async ValueTask<bool> Handle(
		RecordLoginCommand request,
		CancellationToken cancellationToken = default)
	{
		var preExisting = await dbContext.GetUserStreakAsync(request.UserId, cancellationToken);
		var streak = preExisting ?? await dbContext.GetOrCreateUserStreakAsync(request.UserId, cancellationToken);

		if (preExisting is null)
		{
			var existingUserCount = await dbContext.CountUserStreaksAsync(cancellationToken);
			if (existingUserCount < 100)
			{
				await sender.Send(new AwardAchievementCommand(request.UserId, "early-adopter"), cancellationToken);
			}
		}

		var streakBefore = streak.LoginStreak;
		streak.RecordLogin(request.Date);

		if (streakBefore < 7 && streak.LoginStreak >= 7)
		{
			await sender.Send(new AwardAchievementCommand(request.UserId, "on-a-roll-7"), cancellationToken);
		}

		return true;
	}
}
