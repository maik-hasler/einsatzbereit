using Application.Common.Messaging;

namespace Application.Achievements.GetMyAchievements.v1;

internal sealed class GetMyAchievementsQueryHandler(
	IAchievementReadRepository readRepository)
	: IQueryHandler<GetMyAchievementsQuery, List<AchievementSummary>>
{
	public async ValueTask<List<AchievementSummary>> Handle(
		GetMyAchievementsQuery request,
		CancellationToken cancellationToken = default) =>
			await readRepository.GetByUserAsync(request.UserId, cancellationToken);
}
