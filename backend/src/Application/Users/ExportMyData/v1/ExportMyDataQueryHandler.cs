using Application.Achievements;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;

namespace Application.Users.ExportMyData.v1;

internal sealed class ExportMyDataQueryHandler(
	IKeycloakUserService keycloakUserService,
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IAchievementReadRepository achievementReadRepository)
	: IQueryHandler<ExportMyDataQuery, UserDataExportResponse>
{
	public async ValueTask<UserDataExportResponse> Handle(
		ExportMyDataQuery request,
		CancellationToken cancellationToken = default)
	{
		var keycloakUser = await keycloakUserService.GetUserAsync(request.UserId.Value, cancellationToken);
		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		var profile = new UserDataExportProfile(
			keycloakUser.Id,
			keycloakUser.Username,
			keycloakUser.FirstName,
			keycloakUser.LastName,
			keycloakUser.Email,
			user?.AvatarUrl,
			user?.Bio,
			user?.Phone,
			user?.Skills ?? [],
			user?.Languages ?? [],
			user?.PreferredContact?.ToString(),
			user?.PreferredLanguage);

		var engagements = await engagementReadRepository.GetAllByVolunteerAsync(request.UserId, cancellationToken);
		var achievements = await achievementReadRepository.GetByUserAsync(request.UserId, cancellationToken);

		var streak = await dbContext.GetUserStreakAsync(request.UserId, cancellationToken);
		var streakSummary = streak is null
			? new StreakSummary(0, 0)
			: new StreakSummary(streak.LoginStreak, streak.ActivityStreak);

		var memberships = await dbContext.GetMembershipsForUserAsync(request.UserId, cancellationToken);

		return new UserDataExportResponse(profile, engagements, achievements, streakSummary, memberships);
	}
}
