using Application.Achievements.BadgeCatalog;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Achievements;

namespace Application.Achievements.AwardAchievement.v1;

internal sealed class AwardAchievementCommandHandler(
	IApplicationDbContext dbContext,
	IBadgeCatalogService catalogService)
	: ICommandHandler<AwardAchievementCommand, Guid?>
{
	public async ValueTask<Guid?> Handle(
		AwardAchievementCommand request,
		CancellationToken cancellationToken = default)
	{
		var definition = catalogService.FindByKey(request.BadgeKey);
		if (definition is null)
			return null;

		var alreadyAwarded = await dbContext.HasAchievementAsync(
			request.UserId,
			definition.Name,
			cancellationToken);

		if (alreadyAwarded)
			return Guid.Empty;

		var achievement = Achievement.Create(
			request.UserId,
			definition.Type,
			definition.Key,
			definition.Name,
			definition.Description);

		await dbContext.Achievements.AddAsync(achievement, cancellationToken);

		return achievement.Id.Value;
	}
}
