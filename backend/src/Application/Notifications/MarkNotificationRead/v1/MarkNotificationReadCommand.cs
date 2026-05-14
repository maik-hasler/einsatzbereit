using Application.Common.Messaging;
using Domain.Notifications;

namespace Application.Notifications.MarkNotificationRead.v1;

public sealed record MarkNotificationReadCommand(NotificationId NotificationId, Guid RequestingUserId)
	: ICommand<bool>;
