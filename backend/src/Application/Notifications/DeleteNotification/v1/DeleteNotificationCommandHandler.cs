using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Notifications.DeleteNotification.v1;

internal sealed class DeleteNotificationCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteNotificationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteNotificationCommand request,
		CancellationToken cancellationToken = default)
	{
		var notification = await dbContext.Notifications.FindAsync(
			request.NotificationId, cancellationToken);

		if (notification is null || notification.RecipientId.Value != request.RequestingUserId)
			return false;

		dbContext.Notifications.Delete(notification);
		return true;
	}
}
