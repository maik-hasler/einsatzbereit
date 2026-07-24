using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Users;

namespace Application.Users.UploadUserAvatar.v1;

internal sealed class UploadUserAvatarCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<UploadUserAvatarCommand, bool>
{
	public async ValueTask<bool> Handle(
		UploadUserAvatarCommand request,
		CancellationToken cancellationToken = default)
	{
		var contentType = ImageUploadValidator.EnsureValid(request.Content, request.ContentType, "Avatar");

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		if (user is null)
		{
			user = User.Create(request.UserId);
			await dbContext.Users.AddAsync(user, cancellationToken);
		}

		var ext = ImageUploadValidator.GetExtension(contentType);
		var objectKey = $"user-avatars/{request.UserId.Value}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, contentType, cancellationToken);

		user.SetAvatarUrl(url);

		return true;
	}
}
