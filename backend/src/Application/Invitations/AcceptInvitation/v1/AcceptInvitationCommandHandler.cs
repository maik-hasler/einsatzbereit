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

		// Accepting grants full Organizer capability, the only membership tier
		// this app currently gives any real access - matching how
		// CreateOrganizationCommandHandler seats the org's creator. Without this,
		// an accepted invitee is a Keycloak org member with no local
		// OrganizationMembership row and no "organisator" realm role, so every
		// org-scoped endpoint (all gated by that role and/or a per-org Organizer
		// check) stays unreachable for them and they never appear in their own
		// org switcher - accepting would grant no functional capability at all.
		await keycloakOrganizationService.AddMemberAsync(
			invitation.OrganizationId.Value,
			invitation.InviteeId.Value,
			cancellationToken);

		await keycloakOrganizationService.AssignOrganizerRoleAsync(
			invitation.InviteeId.Value,
			cancellationToken);

		var membership = OrganizationMembership.Create(
			invitation.OrganizationId,
			invitation.InviteeId,
			OrganizationMemberRole.Organizer);

		await dbContext.OrganizationMemberships.AddAsync(membership, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
