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
		var streak = await dbContext.GetUserStreakAsync(request.UserId, cancellationToken);

		if (streak is null)
		{
			streak = UserStreak.Create(request.UserId);
			await dbContext.UserStreaks.AddAsync(streak, cancellationToken);

			// #1000: "early-adopter" rewards the first 100 users to ever log in.
			// Counted before this row is saved, so existingUserCount is the number
			// of users who logged in before this one - 0..99 makes this user #1-#100.
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
