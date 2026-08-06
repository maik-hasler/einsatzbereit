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
		// #1677: look up the local row (including shadow-deleted ones) first and
		// bail out before ever calling Keycloak or any other repo - a
		// shadow-deleted user's public profile must 404, not fall back to
		// default field values while still exposing display name/engagement
		// count/badges. A user with no local row yet (never touched their own
		// profile/settings, so User.Create was never called for them - see
		// GetOrCreateUserAsync's callers) is not shadow-deleted and keeps the
		// existing default-field behavior below.
		var user = await dbContext.FindUserIncludingDeletedAsync(request.UserId, cancellationToken);
		if (user is { IsDeleted: true })
			return null;

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

		// PreferredContact/Phone are deliberately not surfaced here (#1028) - this
		// endpoint is AllowAnonymous(), so contact info only ever reaches an
		// organizer through an actual engagement (EngagementSummary), never a
		// cold-view of the public profile by an anonymous visitor.
		return new PublicUserProfileResponse(
			displayName,
			engagementCount,
			badges,
			user?.AvatarUrl,
			user?.Bio,
			user?.Skills ?? [],
			user?.Languages ?? []);
	}
}
