using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.Users;

namespace Application.Users.UploadUserAvatar.v1;

internal sealed class UploadUserAvatarCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<UploadUserAvatarCommand, bool>
{
	private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
	};

	public async ValueTask<bool> Handle(
		UploadUserAvatarCommand request,
		CancellationToken cancellationToken = default)
	{
		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		if (user is null)
		{
			user = User.Create(request.UserId);
			await dbContext.Users.AddAsync(user, cancellationToken);
		}

		var ext = Extensions.GetValueOrDefault(request.ContentType, ".jpg");
		var objectKey = $"user-avatars/{request.UserId.Value}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, request.ContentType, cancellationToken);

		user.SetAvatarUrl(url);

		return true;
	}
}
