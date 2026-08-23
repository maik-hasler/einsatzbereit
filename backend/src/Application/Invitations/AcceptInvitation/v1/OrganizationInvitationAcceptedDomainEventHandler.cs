using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace Application.Invitations.AcceptInvitation.v1;

internal sealed class OrganizationInvitationAcceptedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	ILogger<OrganizationInvitationAcceptedDomainEventHandler> logger)
	: INotificationHandler<OrganizationInvitationAcceptedDomainEvent>
{
	public async Task Handle(
		OrganizationInvitationAcceptedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var invitation = await dbContext.OrganizationInvitations.FindAsync(notification.InvitationId, cancellationToken);
		if (invitation is null)
		{
			logger.LogWarning(
				"Skipping acceptance notification for invitation {InvitationId}: it no longer exists",
				notification.InvitationId.Value);
			return;
		}

		var inAppNotification = Notification.Create(
			invitation.InvitedById,
			NotificationKind.InvitationAccepted,
			notification.InvitationId.Value);
		await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
