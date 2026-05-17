using Application.Common.Messaging;
using Domain.Users;

namespace Application.Achievements.GetUserAchievements.v1;

public sealed record GetUserAchievementsQuery(UserId UserId)
	: IQuery<List<AchievementSummary>>;
