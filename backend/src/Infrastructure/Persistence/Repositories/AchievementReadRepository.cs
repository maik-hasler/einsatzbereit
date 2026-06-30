using Application.Achievements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AchievementReadRepository(
	ApplicationDbContext dbContext)
	: IAchievementReadRepository
{
	public async ValueTask<List<AchievementSummary>> GetByUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await dbContext.AchievementsQuery
			.Where(a => a.UserId == userId)
			.OrderByDescending(a => a.UnlockedAt)
			.Select(a => new AchievementSummary(
				a.Id.Value,
				a.Type.ToString(),
				a.Key,
				a.Name,
				a.Description,
				a.UnlockedAt))
			.ToListAsync(cancellationToken);
}
