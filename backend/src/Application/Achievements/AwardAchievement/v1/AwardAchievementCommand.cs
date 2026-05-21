using Application.Common.Messaging;
using Domain.Users;

namespace Application.Achievements.AwardAchievement.v1;

public sealed record AwardAchievementCommand(
	UserId UserId,
	string BadgeKey)
	: ICommand<Guid?>;
