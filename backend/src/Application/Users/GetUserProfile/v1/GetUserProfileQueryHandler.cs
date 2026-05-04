using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.GetUserProfile.v1;

internal sealed class GetUserProfileQueryHandler(
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<GetUserProfileQuery, MyProfileResponse>
{
	public async ValueTask<MyProfileResponse> Handle(
		GetUserProfileQuery request,
		CancellationToken cancellationToken = default)
	{
		var user = await keycloakUserService.GetUserAsync(
			request.UserId.Value,
			cancellationToken);

		return new MyProfileResponse(
			user.Id,
			user.Username,
			user.FirstName,
			user.LastName,
			user.Email);
	}
}
