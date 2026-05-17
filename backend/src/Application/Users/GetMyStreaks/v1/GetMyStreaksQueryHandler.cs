using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Users.GetMyStreaks.v1;

internal sealed class GetMyStreaksQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetMyStreaksQuery, StreakSummary>
{
	public async ValueTask<StreakSummary> Handle(
		GetMyStreaksQuery request,
		CancellationToken cancellationToken = default)
	{
		var streak = await dbContext.GetUserStreakAsync(request.UserId, cancellationToken);
		return streak is null
			? new StreakSummary(0, 0)
			: new StreakSummary(streak.LoginStreak, streak.ActivityStreak);
	}
}
