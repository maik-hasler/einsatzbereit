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
			new OrganizationId(organizationId), requestingUserId, cancellationToken);

		if (!isOrganizer)
			throw new DomainException("You do not have permission to modify this resource.");
	}
}
