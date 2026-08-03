using Application.Common.Keycloak;
using Application.Organizations.DeleteOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using NSubstitute;

namespace Application.UnitTests.Organizations.DeleteOrganization;

public class OrganizationDeletedDomainEventHandlerTests
{
	private readonly IKeycloakOrganizationService _keycloakOrganizationService = Substitute.For<IKeycloakOrganizationService>();
	private readonly OrganizationDeletedDomainEventHandler _sut;

	public OrganizationDeletedDomainEventHandlerTests()
	{
		_sut = new OrganizationDeletedDomainEventHandler(_keycloakOrganizationService);
	}

	[Test]
	public async Task Handle_ShouldDeleteTheKeycloakOrganization_ForTheEventsOrganizationId(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var domainEvent = new OrganizationDeletedDomainEvent(organizationId);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _keycloakOrganizationService.Received(1).DeleteOrganizationAsync(organizationId.Value, cancellationToken);
	}
}
