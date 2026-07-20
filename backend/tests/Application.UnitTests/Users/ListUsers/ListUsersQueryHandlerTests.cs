using Application.Common.Keycloak;
using Application.Common.Pagination;
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
		_keycloakService
			.ListUsersAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<AdminUserListItem>([], 0, 1, 10));
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
			.ListUsersAsync(null, 1, 10, cancellationToken)
			.Returns(new PagedList<AdminUserListItem>([item], 1, 1, 10));

		// Act
		var result = await _sut.Handle(new ListUsersQuery(null, 1, 10), cancellationToken);

		// Assert
		result.Items.Should().ContainSingle().Which.Should().Be(item);
	}

	[Test]
	public async Task Handle_ShouldPassSearchTerm_ToKeycloakService(
		CancellationToken cancellationToken)
	{
		// Act
		await _sut.Handle(new ListUsersQuery("vera", 1, 10), cancellationToken);

		// Assert
		await _keycloakService.Received(1).ListUsersAsync("vera", 1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListUsersQuery(null, 0, 10), cancellationToken);

		await _keycloakService.Received(1).ListUsersAsync(null, 1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListUsersQuery(null, -5, 10), cancellationToken);

		await _keycloakService.Received(1).ListUsersAsync(null, 1, 10, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListUsersQuery(null, 1, 0), cancellationToken);

		await _keycloakService.Received(1).ListUsersAsync(null, 1, 1, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new ListUsersQuery(null, 1, 5000), cancellationToken);

		await _keycloakService.Received(1).ListUsersAsync(null, 1, 100, cancellationToken);
	}
}
