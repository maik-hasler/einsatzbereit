using Domain.Achievements;

namespace Application.Achievements.BadgeCatalog;

public sealed record BadgeCatalogEntry(
	string Key,
	AchievementType Type,
	string Name,
	string Description,
	bool IsHidden);
