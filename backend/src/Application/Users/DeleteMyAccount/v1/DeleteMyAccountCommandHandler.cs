using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class DeleteMyAccountCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteMyAccountCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteMyAccountCommand request,
		CancellationToken cancellationToken = default)
	{
		await EnsureNotSoleOrganizerOfAnyOrganizationAsync(request.UserId, cancellationToken);

		var engagements = await dbContext.GetEngagementsForVolunteerTrackingAsync(
			request.UserId, cancellationToken);

		foreach (var engagement in engagements)
		{
			// Terminate non-terminal engagements before anonymizing so they
			// stop occupying time-slot capacity and organizers can act on
			// them, instead of leaving a permanently-stuck nameless row (#1140).
			if (!engagement.IsCheckedIn && engagement.Status is EngagementStatus.Pending or EngagementStatus.Confirmed)
				engagement.Withdraw().ThrowIfFailure();

			engagement.Anonymize();
		}

		await dbContext.DeleteNotificationsForRecipientAsync(request.UserId, cancellationToken);
		await dbContext.DeleteUserStreakAsync(request.UserId, cancellationToken);
		await dbContext.DeleteAchievementsForUserAsync(request.UserId, cancellationToken);
		await dbContext.RemoveMembershipsForUserAsync(request.UserId, cancellationToken);
		await dbContext.RemoveDashboardLayoutsForUserAsync(request.UserId, cancellationToken);
		await dbContext.DeleteInvitationsForUserAsync(request.UserId, cancellationToken);
		await dbContext.DeleteSearchAlertForUserAsync(request.UserId, cancellationToken);
		await dbContext.DeleteReportsForReporterAsync(request.UserId, cancellationToken);

		// Reports where this user is the *target* (not the reporter) are
		// moderation history and intentionally survive - but they need a
		// deletion timestamp of their own so AbuseReportRetentionJob has
		// something to measure retention from (#1725).
		var reportsAgainstUser = await dbContext.GetReportHistoryForTargetAsync(
			ReportTargetType.User, request.UserId.Value, cancellationToken);
		foreach (var report in reportsAgainstUser)
			report.MarkTargetDeleted(DateTimeOffset.UtcNow);

		// Must use FindUserIncludingDeletedAsync (IgnoreQueryFilters), not
		// dbContext.Users.FindAsync - a shadow-deleted user's row is hidden by
		// the global !IsDeleted filter and would otherwise silently skip avatar
		// deletion, MarkAccountDeleted, and the Keycloak deletion it triggers (#1725).
		var user = await dbContext.FindUserIncludingDeletedAsync(request.UserId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", "User not found."));

		// The avatar's random object key (issue #1175) can't be reconstructed
		// from the user id, so it has to come from the stored AvatarUrl instead
		// of a guessed extension.
		var avatarObjectKey = user.AvatarUrl is not null
			? fileStorage.GetObjectKeyFromPublicUrl(user.AvatarUrl)
			: null;

		if (avatarObjectKey is not null)
		{
			try
			{
				await fileStorage.DeleteAsync(avatarObjectKey, cancellationToken);
			}
			catch
			{
				// Object may already be gone or storage may be transiently unavailable; continue.
			}
		}

		user.MarkAccountDeleted();
		dbContext.Users.Delete(user);

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
