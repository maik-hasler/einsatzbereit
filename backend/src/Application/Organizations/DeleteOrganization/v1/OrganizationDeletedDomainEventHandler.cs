using Application.Common.Keycloak;
using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.DeleteOrganization.v1;

internal sealed class OrganizationDeletedDomainEventHandler(
	IKeycloakOrganizationService keycloakOrganizationService)
	: INotificationHandler<OrganizationDeletedDomainEvent>
{
	public Task Handle(
		OrganizationDeletedDomainEvent notification,
		CancellationToken cancellationToken) =>
		keycloakOrganizationService.DeleteOrganizationAsync(notification.OrganizationId.Value, cancellationToken);
}
