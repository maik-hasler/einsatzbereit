using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteUserAvatar.v1;

public sealed record DeleteUserAvatarCommand(UserId UserId) : ICommand<bool>;
