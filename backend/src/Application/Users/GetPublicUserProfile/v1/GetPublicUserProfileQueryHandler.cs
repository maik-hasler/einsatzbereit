using Application.Achievements;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Users.GetPublicUserProfile.v1;

internal sealed class GetPublicUserProfileQueryHandler(
	IKeycloakUserService keycloakUserService,
	IApplicationDbContext dbContext,
	IAchievementReadRepository achievementReadRepository)
	: IQueryHandler<GetPublicUserProfileQuery, PublicUserProfileResponse?>
{
	public async ValueTask<PublicUserProfileResponse?> Handle(
		GetPublicUserProfileQuery request,
		CancellationToken cancellationToken = default)
	{
		KeycloakUserProfile keycloakUser;
		try
		{
			keycloakUser = await keycloakUserService.GetUserAsync(
				request.UserId.Value,
				cancellationToken);
		}
		catch
		{
			return null;
		}

		var displayName = keycloakUser.FirstName is not null || keycloakUser.LastName is not null
			? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim()
			: keycloakUser.Username;

		var engagementCount = await dbContext.CountConfirmedEngagementsForVolunteerAsync(
			request.UserId,
			cancellationToken);

		var badges = await achievementReadRepository.GetByUserAsync(
			request.UserId,
			cancellationToken);

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		return new PublicUserProfileResponse(
			displayName,
			engagementCount,
			badges,
			user?.AvatarUrl,
			user?.Bio,
			user?.Skills ?? [],
			user?.Languages ?? [],
			user?.PreferredContact?.ToString());
	}
}
