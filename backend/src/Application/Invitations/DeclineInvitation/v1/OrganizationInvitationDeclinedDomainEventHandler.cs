using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace Application.Invitations.DeclineInvitation.v1;

// Consumer of OrganizationInvitationDeclinedDomainEvent (#1047): DeclineInvitationCommandHandler
// only flips the invitation's Status and raises the event; letting the inviting organizer know
// their invite was declined happens here, dispatched by OutboxProcessorJob like every other
// domain event.
internal sealed class OrganizationInvitationDeclinedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	ILogger<OrganizationInvitationDeclinedDomainEventHandler> logger)
	: INotificationHandler<OrganizationInvitationDeclinedDomainEvent>
{
	public async Task Handle(
		OrganizationInvitationDeclinedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var invitation = await dbContext.OrganizationInvitations.FindAsync(notification.InvitationId, cancellationToken);
		if (invitation is null)
		{
			logger.LogWarning(
				"Skipping decline notification for invitation {InvitationId}: it no longer exists",
				notification.InvitationId.Value);
			return;
		}

		var inAppNotification = Notification.Create(
			invitation.InvitedById,
			NotificationKind.InvitationDeclined,
			notification.InvitationId.Value);
		await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
