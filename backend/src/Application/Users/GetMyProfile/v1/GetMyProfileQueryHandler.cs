using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.GetMyProfile.v1;

internal sealed class GetMyProfileQueryHandler(
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<GetMyProfileQuery, MyProfileResponse>
{
	public async ValueTask<MyProfileResponse> Handle(
		GetMyProfileQuery request,
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
