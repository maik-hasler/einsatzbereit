using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteSearchAlert.v1;

public sealed record DeleteSearchAlertCommand(UserId UserId)
	: ICommand<bool>;
