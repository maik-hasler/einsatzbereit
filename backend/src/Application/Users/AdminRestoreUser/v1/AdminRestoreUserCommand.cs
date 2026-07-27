using Application.Common.Messaging;

namespace Application.Users.AdminRestoreUser.v1;

public sealed record AdminRestoreUserCommand(
	Guid UserId)
	: ICommand<bool>;
