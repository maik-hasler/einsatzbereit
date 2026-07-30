using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Invitations.AcceptInvitation.v1;

internal sealed class AcceptInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<AcceptInvitationCommand, bool>
{
	public async ValueTask<bool> Handle(
		AcceptInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		var invitation = await dbContext.OrganizationInvitations.FindAsync(request.InvitationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("OrganizationInvitation.NotFound", "Invitation not found."));

		if (invitation.InviteeId != request.UserId)
			throw new ResultFailureException(Error.Forbidden("OrganizationInvitation.NotRecipient", "You are not the recipient of this invitation."));

		invitation.Accept().ThrowIfFailure();

		await keycloakOrganizationService.AddMemberAsync(
			invitation.OrganizationId.Value,
			invitation.InviteeId.Value,
			cancellationToken);

		// The realm "organisator" role is only needed for the Organizer tier -
		// it gates org-management endpoints as a coarse precondition on top of
		// the real per-org OrganizationMembership.Role check. A plain Member
		// never needs it.
		if (invitation.IntendedRole == OrganizationMemberRole.Organizer)
		{
			await keycloakOrganizationService.AssignOrganizerRoleAsync(
				invitation.InviteeId.Value,
				cancellationToken);
		}

		var membership = OrganizationMembership.Create(
			invitation.OrganizationId,
			invitation.InviteeId,
			invitation.IntendedRole);

		await dbContext.OrganizationMemberships.AddAsync(membership, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
