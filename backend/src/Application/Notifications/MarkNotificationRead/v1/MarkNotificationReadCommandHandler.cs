using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Notifications.MarkNotificationRead.v1;

internal sealed class MarkNotificationReadCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<MarkNotificationReadCommand, bool>
{
	public async ValueTask<bool> Handle(
		MarkNotificationReadCommand request,
		CancellationToken cancellationToken = default)
	{
		var notification = await dbContext.Notifications.FindAsync(
			request.NotificationId, cancellationToken);

		if (notification is null || notification.RecipientId.Value != request.RequestingUserId)
			return false;

		notification.MarkRead();
		return true;
	}
}
