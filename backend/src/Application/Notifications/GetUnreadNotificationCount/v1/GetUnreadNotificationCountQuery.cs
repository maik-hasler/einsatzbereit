using Application.Common.Messaging;
using Domain.Users;

namespace Application.Notifications.GetUnreadNotificationCount.v1;

public sealed record GetUnreadNotificationCountQuery(UserId RecipientId)
	: IQuery<int>;
