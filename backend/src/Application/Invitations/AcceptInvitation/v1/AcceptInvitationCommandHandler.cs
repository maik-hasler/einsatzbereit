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

		// A double-accept (two in-flight requests for the same invitation) races
		// on organization_membership's unique index - without this check both
		// pass invitation.Accept() and both try to insert a membership row, and
		// the loser previously surfaced a raw 23505 as an unhandled 500 even
		// though the invitee ends up a member either way (#1202). The first
		// request through still wins the race on invitation.Accept() itself
		// (Status guard below), which - combined with OrganizationInvitation's
		// concurrency token (#1196) - is what makes the second request observe
		// either an existing membership here or a 409 from the concurrency check
		// on save, never a 500.
		var existingMembership = await dbContext.GetMembershipAsync(
			invitation.OrganizationId, invitation.InviteeId, cancellationToken);
		if (existingMembership is not null)
			return true;

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
