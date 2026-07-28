using Application.Common.Messaging;
using Domain.Users;

namespace Application.Notifications.GetMyNotifications.v1;

public sealed record GetMyNotificationsQuery(UserId RecipientId, DateTimeOffset? Before)
	: IQuery<NotificationsPage>;
