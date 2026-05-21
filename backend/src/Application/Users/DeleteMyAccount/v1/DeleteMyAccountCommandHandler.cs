using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class DeleteMyAccountCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: ICommandHandler<DeleteMyAccountCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteMyAccountCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagements = await dbContext.GetEngagementsForVolunteerTrackingAsync(
			request.UserId, cancellationToken);

		foreach (var engagement in engagements)
			engagement.Anonymize();

		await dbContext.DeleteNotificationsForRecipientAsync(request.UserId, cancellationToken);

		await keycloakUserService.DeleteUserAsync(request.UserId.Value, cancellationToken);

		return true;
	}
}
