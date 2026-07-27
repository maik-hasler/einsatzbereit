using Application.Common.Exceptions;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;

namespace Application.Common.Authorization;

public static class OwnershipGuard
{
	public static async Task EnsureIsOrganizerAsync(
		IApplicationDbContext dbContext,
		Guid organizationId,
		UserId requestingUserId,
		CancellationToken cancellationToken)
	{
		var isOrganizer = await dbContext.IsOrganizerAsync(
			OrganizationId.Create(organizationId).GetValueOrThrow(), requestingUserId, cancellationToken);

		if (!isOrganizer)
			throw new ResultFailureException(Error.Forbidden(
				"Organization.NotOrganizer",
				"You do not have permission to modify this resource."));
	}

	// Lets a platform admin act on an organization's resources without being a
	// member (e.g. removing content flagged via a moderation report - #1075),
	// while every other call site keeps the plain organizer-only check.
	public static async Task EnsureIsOrganizerOrAdminAsync(
		IApplicationDbContext dbContext,
		Guid organizationId,
		UserId requestingUserId,
		bool isAdmin,
		CancellationToken cancellationToken)
	{
		if (isAdmin)
			return;

		await EnsureIsOrganizerAsync(dbContext, organizationId, requestingUserId, cancellationToken);
	}
}
