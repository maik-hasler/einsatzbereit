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
		await dbContext.DeleteReportsForReporterAsync(request.UserId, cancellationToken);

		var reportsAgainstUser = await dbContext.GetReportHistoryForTargetAsync(
			ReportTargetType.User, request.UserId.Value, cancellationToken);
		foreach (var report in reportsAgainstUser)
			report.MarkTargetDeleted(DateTimeOffset.UtcNow);

		var user = await dbContext.FindUserIncludingDeletedAsync(request.UserId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("User.NotFound", "User not found."));

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
