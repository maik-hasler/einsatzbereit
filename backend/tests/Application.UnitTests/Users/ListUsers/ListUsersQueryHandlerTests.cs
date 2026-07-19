using Application.Common.Keycloak;
using Application.Users.ListUsers.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Users.ListUsers;

public class ListUsersQueryHandlerTests
{
	private readonly IKeycloakUserService _keycloakService = Substitute.For<IKeycloakUserService>();
	private readonly ListUsersQueryHandler _sut;

	public ListUsersQueryHandlerTests()
	{
		_sut = new ListUsersQueryHandler(_keycloakService);
	}

	[Test]
	public async Task Handle_ShouldReturnUsers_FromKeycloakService(
		CancellationToken cancellationToken)
	{
		// Arrange
		var item = new AdminUserListItem(
			Guid.NewGuid(), "vera", "Vera", "Volunteer", "vera@example.com", true, ["user"]);

		_keycloakService
			.ListUsersAsync(null, Arg.Any<int>(), cancellationToken)
			.Returns((IReadOnlyList<AdminUserListItem>)[item]);

		// Act
		var result = await _sut.Handle(new ListUsersQuery(null), cancellationToken);

		// Assert
		result.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldPassSearchTerm_ToKeycloakService(
		CancellationToken cancellationToken)
	{
		// Arrange
		_keycloakService
			.ListUsersAsync("vera", Arg.Any<int>(), cancellationToken)
			.Returns((IReadOnlyList<AdminUserListItem>)[]);

		// Act
		await _sut.Handle(new ListUsersQuery("vera"), cancellationToken);

		// Assert
		await _keycloakService.Received(1).ListUsersAsync("vera", Arg.Any<int>(), cancellationToken);
	}
}
