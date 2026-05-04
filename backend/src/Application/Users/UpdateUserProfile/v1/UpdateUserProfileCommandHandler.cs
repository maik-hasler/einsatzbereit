using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.UpdateUserProfile.v1;

internal sealed class UpdateUserProfileCommandHandler(
	IKeycloakUserService keycloakUserService)
	: ICommandHandler<UpdateUserProfileCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateUserProfileCommand request,
		CancellationToken cancellationToken = default)
	{
		await keycloakUserService.UpdateUserAsync(
			request.UserId.Value,
			request.FirstName,
			request.LastName,
			cancellationToken);

		return true;
	}
}
