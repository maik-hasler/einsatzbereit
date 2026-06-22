using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.UploadUserAvatar.v1;

public sealed record UploadUserAvatarCommand(
	UserId UserId,
	byte[] Content,
	string ContentType)
	: ICommand<bool>;
