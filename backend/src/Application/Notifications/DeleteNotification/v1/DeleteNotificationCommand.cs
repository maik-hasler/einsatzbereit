using Application.Common.Messaging;
using Domain.Notifications;

namespace Application.Notifications.DeleteNotification.v1;

public sealed record DeleteNotificationCommand(NotificationId NotificationId, Guid RequestingUserId)
	: ICommand<bool>;
