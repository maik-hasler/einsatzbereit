using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Primitives;

namespace Application.Organizations.ResendInvitation.v1;

internal sealed class ResendInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService)
	: ICommandHandler<ResendInvitationCommand, bool>
{
	public async ValueTask<bool> Handle(
		ResendInvitationCommand request,
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

		var invitee = await keycloakUserService.GetUserAsync(invitation.InviteeId.Value, cancellationToken);

		invitation.Resend(DateTimeOffset.UtcNow).ThrowIfFailure();

		// A fresh, unread notification - the original one (if still unread) stays
		// as-is, but the invitee should see this bump in their bell dropdown the
		// same way they would for a brand new invitation.
		var notification = Notification.Create(
			invitation.InviteeId,
			NotificationKind.InvitationReceived,
			invitation.Id.Value);
		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		await emailService.SendAsync(
			invitee.Email,
			"You've been invited to join an organization",
			$"Hello {invitee.FirstName ?? invitee.Username},\n\n" +
			$"You've been invited to join \"{invitation.OrganizationName}\" on Einsatzbereit.\n\n" +
			$"Log in to your account to accept or decline the invitation.\n\n" +
			$"Einsatzbereit",
			cancellationToken);

		return true;
	}
}
