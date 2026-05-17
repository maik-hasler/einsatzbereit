using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.RecordLogin.v1;

public sealed record RecordLoginCommand(UserId UserId, DateOnly Date)
	: ICommand<bool>;
