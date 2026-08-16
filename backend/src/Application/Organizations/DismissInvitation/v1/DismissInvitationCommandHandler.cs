using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.DismissInvitation.v1;

internal sealed class DismissInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork)
	: ICommandHandler<DismissInvitationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DismissInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var invitation = await dbContext.OrganizationInvitations.FindAsync(request.InvitationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("OrganizationInvitation.NotFound", "Invitation not found."));

		if (invitation.OrganizationId != request.OrganizationId)
			throw new ResultFailureException(Error.Validation("OrganizationInvitation.WrongOrganization", "Invitation does not belong to this organization."));

		// #1040: Pending must be dismissible too, not just Declined/Expired - an
		// organizer who invited the wrong person otherwise has no way to revoke
		// it, and accepting grants full Organizer capability (#826). Only a
		// finalized Accepted invitation - now a real membership row, removed via
		// RemoveMember instead - is excluded.
		if (invitation.Status == InvitationStatus.Accepted)
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.AlreadyAccepted", "Accepted invitations cannot be dismissed."));

		// #1919: an organizer dismissing (revoking) an invitation deletes the
		// row outright, so the invitee's InvitationReceived notification would
		// otherwise point at an invitation that no longer exists at all.
		await dbContext.DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);

		dbContext.OrganizationInvitations.Delete(invitation);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
