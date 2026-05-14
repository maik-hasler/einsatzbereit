using Application.Common.Messaging;
using Domain.Users;

namespace Application.Notifications.MarkAllNotificationsRead.v1;

public sealed record MarkAllNotificationsReadCommand(UserId RecipientId)
	: ICommand<int>;
