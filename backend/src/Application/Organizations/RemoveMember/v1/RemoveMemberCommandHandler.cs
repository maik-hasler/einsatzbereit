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
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();
		var userId = UserId.Create(request.UserId).GetValueOrThrow();

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

		return true;
	}
}
