namespace Application.Achievements;

public sealed record AchievementSummary(
	Guid Id,
	string Type,
	string? Key,
	string Name,
	string Description,
	DateTimeOffset UnlockedAt);
