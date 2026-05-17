using Application.Common.Messaging;
using Domain.Achievements;
using Domain.Users;

namespace Application.Achievements.AwardAchievement.v1;

public sealed record AwardAchievementCommand(
	UserId UserId,
	AchievementType Type,
	string Name,
	string Description)
	: ICommand<Guid>;
