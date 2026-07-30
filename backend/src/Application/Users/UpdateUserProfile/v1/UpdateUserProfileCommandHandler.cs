using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.UpdateUserProfile.v1;

internal sealed class UpdateUserProfileCommandHandler(
	IKeycloakUserService keycloakUserService,
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork)
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

		var user = await dbContext.Users.FindAsync(request.UserId, cancellationToken);

		if (user is null)
		{
			user = User.Create(request.UserId);
			await dbContext.Users.AddAsync(user, cancellationToken);
		}

		user.ChangeBio(request.Bio);
		user.SetPhone(request.Phone);
		user.UpdateSkills(request.Skills);
		user.UpdateLanguages(request.Languages);
		user.SetPreferredContact(request.PreferredContactValue);
		user.SetPreferredLanguage(request.PreferredLanguage);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
