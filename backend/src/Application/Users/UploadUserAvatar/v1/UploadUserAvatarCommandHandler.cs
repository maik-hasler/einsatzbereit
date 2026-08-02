using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;

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

		// #1148: idempotent get-or-create instead of a check-then-Add that could
		// race a concurrent first-time call for the same user.
		var user = await dbContext.GetOrCreateUserAsync(request.UserId, preferredLanguage: null, cancellationToken);

		var ext = ImageUploadValidator.GetExtension(contentType);
		var objectKey = $"user-avatars/{request.UserId.Value}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, contentType, cancellationToken);

		user.SetAvatarUrl(url);

		return true;
	}
}
