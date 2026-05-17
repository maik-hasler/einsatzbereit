using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.GetUserProfile.v1;

internal sealed class GetUserProfileQueryHandler(
	IKeycloakUserService keycloakUserService,
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork)
	: IQueryHandler<GetUserProfileQuery, MyProfileResponse>
{
	public async ValueTask<MyProfileResponse> Handle(
		GetUserProfileQuery request,
		CancellationToken cancellationToken = default)
	{
		var keycloakUser = await keycloakUserService.GetUserAsync(
			request.UserId.Value,
			cancellationToken);

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		if (user is null)
		{
			user = User.Create(request.UserId);
			await dbContext.Users.AddAsync(user, cancellationToken);
			await unitOfWork.SaveChangesAsync(cancellationToken);
		}

		return new MyProfileResponse(
			keycloakUser.Id,
			keycloakUser.Username,
			keycloakUser.FirstName,
			keycloakUser.LastName,
			keycloakUser.Email,
			user.Bio,
			user.Skills,
			user.Languages,
			user.PreferredContact?.ToString());
	}
}
