using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;

namespace Application.Organizations.RemoveMember.v1;

internal sealed class RemoveMemberCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<RemoveMemberCommand, bool>
{
	public async ValueTask<bool> Handle(
		RemoveMemberCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();
		var userId = UserId.Create(request.UserId).GetValueOrThrow();

		// Leaving is a self-service action available to any tier, not an
		// org-management action - only removing someone else requires Organizer.
		if (userId == request.RequestingUserId)
		{
			await OwnershipGuard.EnsureIsMemberAsync(
				dbContext,
				request.OrganizationId,
				request.RequestingUserId,
				cancellationToken);
		}
		else
		{
			await OwnershipGuard.EnsureIsOrganizerAsync(
				dbContext,
				request.OrganizationId,
				request.RequestingUserId,
				cancellationToken);
		}

		var isOrganizer = await dbContext.IsOrganizerAsync(organizationId, userId, cancellationToken);

		if (isOrganizer)
		{
			var organizerCount = await dbContext.CountOrganizersAsync(organizationId, cancellationToken);

			if (organizerCount <= 1)
				throw new ResultFailureException(Error.Conflict(
					"Organization.SoleOrganizer",
					"Conflict: you are the only organizer of this organization. Delete the organization instead of leaving it."));
		}

		await keycloakOrganizationService.RemoveMemberAsync(
			request.OrganizationId, request.UserId, cancellationToken);

		await dbContext.RemoveMembershipAsync(
			organizationId,
			userId,
			cancellationToken);

		if (isOrganizer)
		{
			// The role is realm-wide, not per-organization (see #1386), so it can
			// only be revoked once the removed user organizes no other
			// organization (#1677) - otherwise this removal would also lock them
			// out of that other org. RemoveMembershipAsync above already deleted
			// this org's membership row, so unlike ChangeMemberRoleCommandHandler's
			// still-in-memory demotion, no exclusion filter is needed here.
			var remainingOrganizerOrgs = await dbContext.GetOrganizerOrganizationsAsync(userId, cancellationToken);

			if (remainingOrganizerOrgs.Count == 0)
				await keycloakOrganizationService.RevokeOrganizerRoleAsync(userId.Value, cancellationToken);
		}

		return true;
	}
}
