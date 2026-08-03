using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.DeleteOrganization.v1;

// Consumer of OrganizationDeletedDomainEvent (#1218): DeleteOrganizationCommandHandler
// only removes local rows and raises the event; the Keycloak organization deletion -
// the irreversible, external half of the operation - happens here, dispatched by
// OutboxProcessorJob after the triggering transaction has already committed, so a
// failed commit never leaves the Keycloak organization gone while the local rollback
// restores everything (see backend/AGENTS.md's "Domain events" section). A transient
// Keycloak failure just leaves this outbox message unprocessed for the next poll cycle
// to retry.
internal sealed class OrganizationDeletedDomainEventHandler(
	IKeycloakOrganizationService keycloakOrganizationService)
	: INotificationHandler<OrganizationDeletedDomainEvent>
{
	public Task Handle(
		OrganizationDeletedDomainEvent notification,
		CancellationToken cancellationToken) =>
		keycloakOrganizationService.DeleteOrganizationAsync(notification.OrganizationId.Value, cancellationToken);
}
