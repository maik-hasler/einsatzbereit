using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.UpdateMyProfile.v1;

internal sealed class UpdateMyProfileCommandHandler(
	IKeycloakUserService keycloakUserService)
	: ICommandHandler<UpdateMyProfileCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateMyProfileCommand request,
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
