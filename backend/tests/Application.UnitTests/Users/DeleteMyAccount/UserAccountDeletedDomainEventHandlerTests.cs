using Application.Common.Keycloak;
using Application.Users.DeleteMyAccount.v1;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Users.DeleteMyAccount;

public class UserAccountDeletedDomainEventHandlerTests
{
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly UserAccountDeletedDomainEventHandler _sut;

	public UserAccountDeletedDomainEventHandlerTests()
	{
		_sut = new UserAccountDeletedDomainEventHandler(_keycloakUserService);
	}

	[Test]
	public async Task Handle_ShouldDeleteTheKeycloakUser_MatchingTheEventsUserId(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = UserId.New();
		var notification = new UserAccountDeletedDomainEvent(userId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _keycloakUserService.Received(1).DeleteUserAsync(userId.Value, cancellationToken);
	}
}
