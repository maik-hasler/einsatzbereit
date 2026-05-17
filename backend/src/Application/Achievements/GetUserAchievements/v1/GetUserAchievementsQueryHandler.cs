using Application.Common.Messaging;

namespace Application.Achievements.GetUserAchievements.v1;

internal sealed class GetUserAchievementsQueryHandler(
	IAchievementReadRepository readRepository)
	: IQueryHandler<GetUserAchievementsQuery, List<AchievementSummary>>
{
	public async ValueTask<List<AchievementSummary>> Handle(
		GetUserAchievementsQuery request,
		CancellationToken cancellationToken = default) =>
			await readRepository.GetByUserAsync(request.UserId, cancellationToken);
}
