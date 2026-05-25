using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

public sealed record DeleteMyAccountCommand(UserId UserId) : ICommand<bool>;
