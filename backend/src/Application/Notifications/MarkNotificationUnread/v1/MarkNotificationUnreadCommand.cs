using Application.Common.Messaging;
using Domain.Notifications;

namespace Application.Notifications.MarkNotificationUnread.v1;

public sealed record MarkNotificationUnreadCommand(NotificationId NotificationId, Guid RequestingUserId)
	: ICommand<bool>;
