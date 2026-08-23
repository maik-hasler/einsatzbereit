using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

internal sealed class UserAccountDeletedDomainEventHandler(
	IKeycloakUserService keycloakUserService)
	: INotificationHandler<UserAccountDeletedDomainEvent>
{
	public Task Handle(
		UserAccountDeletedDomainEvent notification,
		CancellationToken cancellationToken) =>
		keycloakUserService.DeleteUserAsync(notification.UserId.Value, cancellationToken);
}
