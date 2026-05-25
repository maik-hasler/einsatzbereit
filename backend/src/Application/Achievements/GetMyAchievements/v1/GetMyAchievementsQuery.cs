using Application.Common.Messaging;
using Domain.Users;

namespace Application.Achievements.GetMyAchievements.v1;

public sealed record GetMyAchievementsQuery(UserId UserId)
	: IQuery<List<AchievementSummary>>;
