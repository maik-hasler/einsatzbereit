using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.AdminShadowDeleteUser.v1;

public sealed record AdminShadowDeleteUserCommand(
	Guid UserId,
	UserId AdminUserId)
	: ICommand<bool>;
