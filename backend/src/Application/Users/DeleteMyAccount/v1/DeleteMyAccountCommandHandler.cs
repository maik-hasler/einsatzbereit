using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class DeleteMyAccountCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
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

			user.Delete();
			dbContext.Users.Delete(user);
		}
		else
		{
			// No local profile row exists - User.Create() only runs lazily, from the
			// first profile-touching endpoint (avatar/bio/notification-prefs/...), so a
			// Keycloak-only account can reach here with nothing to find. The Keycloak
			// deletion (#1218) still must run, and still only after this transaction
			// commits - but there is no tracked User to carry UserDeletedDomainEvent
			// through ConvertDomainEventsToOutboxMessagesInterceptor. Add()-then-Delete()
			// in one tick would just detach the entry before the interceptor ever sees it
			// (EF collapses Added+Removed straight to Detached), so this placeholder is
			// persisted long enough - one explicit SaveChangesAsync - for the interceptor
			// to queue the event, then removed; net rows in `users` is still zero, all
			// inside the one transaction TransactionPipelineBehavior wraps this command in.
			var placeholder = User.Create(request.UserId);
			placeholder.Delete();
			await dbContext.Users.AddAsync(placeholder, cancellationToken);
			await unitOfWork.SaveChangesAsync(cancellationToken);
			dbContext.Users.Delete(placeholder);
		}

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
