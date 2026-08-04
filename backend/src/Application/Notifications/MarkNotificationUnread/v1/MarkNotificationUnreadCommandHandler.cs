using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Notifications.MarkNotificationUnread.v1;

internal sealed class MarkNotificationUnreadCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<MarkNotificationUnreadCommand, bool>
{
	public async ValueTask<bool> Handle(
		MarkNotificationUnreadCommand request,
		CancellationToken cancellationToken = default)
	{
		var notification = await dbContext.Notifications.FindAsync(
			request.NotificationId, cancellationToken);

		if (notification is null || notification.RecipientId.Value != request.RequestingUserId)
			return false;

		notification.MarkUnread();
		return true;
	}
}
