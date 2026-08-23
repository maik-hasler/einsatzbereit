using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace Application.Invitations.DeclineInvitation.v1;

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
