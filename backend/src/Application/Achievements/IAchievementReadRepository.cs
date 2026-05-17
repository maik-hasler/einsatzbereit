using Domain.Users;

namespace Application.Achievements;

public interface IAchievementReadRepository
{
	ValueTask<List<AchievementSummary>> GetByUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);
}
