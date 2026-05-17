using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Achievements;

namespace Application.Achievements.AwardAchievement.v1;

internal sealed class AwardAchievementCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<AwardAchievementCommand, Guid>
{
	public async ValueTask<Guid> Handle(
		AwardAchievementCommand request,
		CancellationToken cancellationToken = default)
	{
		var alreadyAwarded = await dbContext.HasAchievementAsync(
			request.UserId,
			request.Type,
			cancellationToken);

		if (alreadyAwarded)
			return Guid.Empty;

		var achievement = Achievement.Create(
			request.UserId,
			request.Type,
			request.Name,
			request.Description);

		await dbContext.Achievements.AddAsync(achievement, cancellationToken);

		return achievement.Id.Value;
	}
}
