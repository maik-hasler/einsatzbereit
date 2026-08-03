using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.DeleteMyAccount.v1;

// Consumer of UserDeletedDomainEvent (#1218): DeleteMyAccountCommandHandler only
// removes local rows and raises the event; the Keycloak account deletion - the
// irreversible, external half of the operation - happens here, dispatched by
// OutboxProcessorJob after the triggering transaction has already committed, so a
// failed commit never leaves the Keycloak identity gone while the local rollback
// restores everything (see backend/AGENTS.md's "Domain events" section). A
// transient Keycloak failure just leaves this outbox message unprocessed for the
// next poll cycle to retry.
internal sealed class UserDeletedDomainEventHandler(
	IKeycloakUserService keycloakUserService)
	: INotificationHandler<UserDeletedDomainEvent>
{
	public Task Handle(
		UserDeletedDomainEvent notification,
		CancellationToken cancellationToken) =>
		keycloakUserService.DeleteUserAsync(notification.UserId.Value, cancellationToken);
}
