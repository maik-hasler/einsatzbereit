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

		var members = await keycloakOrganizationService.GetMembersAsync(
			request.OrganizationId, cancellationToken);

		if (members.Count == 1 && members[0].UserId == request.UserId)
			throw new ResultFailureException(Error.Conflict(
				"Organization.SoleMember",
				"Conflict: you are the only member of this organization. Delete the organization instead of leaving it."));

		await keycloakOrganizationService.RemoveMemberAsync(
			request.OrganizationId, request.UserId, cancellationToken);

		await dbContext.RemoveMembershipAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(),
			UserId.Create(request.UserId).GetValueOrThrow(),
			cancellationToken);

		return true;
	}
}
