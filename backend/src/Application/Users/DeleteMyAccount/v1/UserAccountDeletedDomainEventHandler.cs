using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

// Consumer of UserAccountDeletedDomainEvent (#1141): the Keycloak identity is
// irreversible once deleted, so it must only happen after the local deletion
// has committed - dispatched by OutboxProcessorJob like every other domain
// event, so a transient Keycloak failure is retried on the next poll cycle
// instead of leaving the account gone from Keycloak but still present locally.
internal sealed class UserAccountDeletedDomainEventHandler(
	IKeycloakUserService keycloakUserService)
	: INotificationHandler<UserAccountDeletedDomainEvent>
{
	public Task Handle(
		UserAccountDeletedDomainEvent notification,
		CancellationToken cancellationToken) =>
		keycloakUserService.DeleteUserAsync(notification.UserId.Value, cancellationToken);
}
