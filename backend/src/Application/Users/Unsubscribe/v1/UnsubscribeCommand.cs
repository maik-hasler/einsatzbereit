using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.Unsubscribe.v1;

public sealed record UnsubscribeCommand(
	UserId UserId,
	Guid Token,
	EmailNotificationType Type)
	: ICommand<bool>;
