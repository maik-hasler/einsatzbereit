using Application.Common.Keycloak;
using Application.Users.DeleteMyAccount.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.DeleteMyAccount;

public class UserDeletedDomainEventHandlerTests
{
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly UserDeletedDomainEventHandler _sut;

	public UserDeletedDomainEventHandlerTests()
	{
		_sut = new UserDeletedDomainEventHandler(_keycloakUserService);
	}

	[Test]
	public async Task Handle_ShouldDeleteTheKeycloakAccount_ForTheEventsUserId(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var domainEvent = new UserDeletedDomainEvent(userId);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _keycloakUserService.Received(1).DeleteUserAsync(userId.Value, cancellationToken);
	}
}
