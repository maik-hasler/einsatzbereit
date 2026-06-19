using Application.Achievements;

namespace Application.Users.GetPublicUserProfile.v1;

public sealed record PublicUserProfileResponse(
	string DisplayName,
	int EngagementCount,
	IReadOnlyList<AchievementSummary> Badges);
