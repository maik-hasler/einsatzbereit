using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.AdminRestoreUser.v1;

public sealed record AdminRestoreUserCommand(
	Guid UserId,
	UserId AdminUserId)
	: ICommand<bool>;
