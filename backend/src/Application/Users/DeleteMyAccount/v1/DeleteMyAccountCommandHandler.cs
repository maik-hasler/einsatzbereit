using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class DeleteMyAccountCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteMyAccountCommand, bool>
{
	private static readonly string[] AvatarExtensions = [".jpg", ".png", ".webp"];

	public async ValueTask<bool> Handle(
		DeleteMyAccountCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagements = await dbContext.GetEngagementsForVolunteerTrackingAsync(
			request.UserId, cancellationToken);

		foreach (var engagement in engagements)
			engagement.Anonymize();

		await dbContext.DeleteNotificationsForRecipientAsync(request.UserId, cancellationToken);

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);
		if (user is not null)
		{
			foreach (var ext in AvatarExtensions)
			{
				try
				{
					await fileStorage.DeleteAsync($"user-avatars/{request.UserId.Value}{ext}", cancellationToken);
				}
				catch
				{
					// Object may not exist for this extension; continue
				}
			}

			dbContext.Users.Delete(user);
		}

		await keycloakUserService.DeleteUserAsync(request.UserId.Value, cancellationToken);

		return true;
	}
}
