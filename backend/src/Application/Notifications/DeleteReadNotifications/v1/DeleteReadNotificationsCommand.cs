using Application.Common.Messaging;
using Domain.Users;

namespace Application.Notifications.DeleteReadNotifications.v1;

public sealed record DeleteReadNotificationsCommand(UserId RecipientId)
	: ICommand<int>;
