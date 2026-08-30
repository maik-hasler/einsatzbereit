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
				"You do not have permission to do this - only organizers of this organization can."));
	}

	public static async Task EnsureIsMemberAsync(
		IApplicationDbContext dbContext,
		Guid organizationId,
		UserId requestingUserId,
		CancellationToken cancellationToken)
	{
		var isMember = await dbContext.IsMemberAsync(
			OrganizationId.Create(organizationId).GetValueOrThrow(), requestingUserId, cancellationToken);

		if (!isMember)
			throw new ResultFailureException(Error.Forbidden(
				"Organization.NotMember",
				"You do not have permission to view this resource."));
	}
}
