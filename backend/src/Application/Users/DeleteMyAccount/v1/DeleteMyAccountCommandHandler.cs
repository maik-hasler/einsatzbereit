using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class DeleteMyAccountCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteMyAccountCommand, bool>
{
	private static readonly string[] AvatarExtensions = [".jpg", ".png", ".webp"];

	public async ValueTask<bool> Handle(
		DeleteMyAccountCommand request,
		CancellationToken cancellationToken = default)
	{
		await EnsureNotSoleOrganizerOfAnyOrganizationAsync(request.UserId, cancellationToken);

		var engagements = await dbContext.GetEngagementsForVolunteerTrackingAsync(
			request.UserId, cancellationToken);

		foreach (var engagement in engagements)
			engagement.Anonymize();

		await dbContext.DeleteNotificationsForRecipientAsync(request.UserId, cancellationToken);
		await dbContext.DeleteUserStreakAsync(request.UserId, cancellationToken);
		await dbContext.DeleteAchievementsForUserAsync(request.UserId, cancellationToken);
		await dbContext.RemoveMembershipsForUserAsync(request.UserId, cancellationToken);
		await dbContext.RemoveDashboardLayoutsForUserAsync(request.UserId, cancellationToken);
		await dbContext.DeleteInvitationsForUserAsync(request.UserId, cancellationToken);

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);
		if (user is not null)
		{
			foreach (var ext in AvatarExtensions)
			{
				try
				{
					await fileStorage.DeleteAsync($"user-avatars/{request.UserId.Value}{ext}", cancellationToken);
				}
				catch
				{
					// Object may not exist for this extension; continue
				}
			}

			dbContext.Users.Delete(user);
		}

		await keycloakUserService.DeleteUserAsync(request.UserId.Value, cancellationToken);

		return true;
	}

	// Wiping this user's organization_membership row for an organization
	// where they are the sole organizer would leave it with no one who can
	// manage it - the same situation RemoveMemberCommandHandler already
	// refuses to create when an organizer tries to leave. Checked first,
	// before any destructive step below, so a blocked deletion has no
	// side effects at all.
	private async Task EnsureNotSoleOrganizerOfAnyOrganizationAsync(
		UserId userId,
		CancellationToken cancellationToken)
	{
		var organizerOrganizations = await dbContext.GetOrganizerOrganizationsAsync(userId, cancellationToken);

		var soleOrganizerOrganizationNames = new List<string>();
		foreach (var organization in organizerOrganizations)
		{
			var organizerCount = await dbContext.CountOrganizersAsync(organization.Id, cancellationToken);
			if (organizerCount <= 1)
				soleOrganizerOrganizationNames.Add(organization.Name);
		}

		if (soleOrganizerOrganizationNames.Count > 0)
		{
			var names = string.Join(", ", soleOrganizerOrganizationNames.Select(name => $"'{name}'"));
			throw new ResultFailureException(Error.Conflict(
				"User.SoleOrganizerOfOrganizations",
				$"Conflict: you are the sole organizer of the following organization(s): {names}. " +
				"Transfer ownership to another organizer or delete these organizations before deleting your account."));
		}
	}
}
