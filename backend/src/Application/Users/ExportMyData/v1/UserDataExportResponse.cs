using Application.Achievements;
using Application.Engagements;
using Application.Organizations;

namespace Application.Users.ExportMyData.v1;

public sealed record UserDataExportResponse(
	UserDataExportProfile Profile,
	IReadOnlyList<EngagementSummary> Engagements,
	IReadOnlyList<AchievementSummary> Achievements,
	StreakSummary Streak,
	IReadOnlyList<OrganizationMembershipSummary> OrganizationMemberships);

public sealed record UserDataExportProfile(
	Guid Id,
	string Username,
	string? FirstName,
	string? LastName,
	string Email,
	string? AvatarUrl,
	string? Bio,
	string? Phone,
	IReadOnlyList<string> Skills,
	IReadOnlyList<string> Languages,
	string? PreferredContact,
	string? PreferredLanguage);
