using Application.Notifications;
using Application.Notifications.GetUnreadNotificationCount.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.GetUnreadNotificationCount;

public class GetUnreadNotificationCountQueryHandlerTests
{
	private readonly INotificationReadRepository _readRepository = Substitute.For<INotificationReadRepository>();
	private readonly GetUnreadNotificationCountQueryHandler _sut;

	public GetUnreadNotificationCountQueryHandlerTests()
	{
		_sut = new GetUnreadNotificationCountQueryHandler(_readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnCountFromRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		_readRepository.CountUnreadByRecipientAsync(recipientId, cancellationToken).Returns(7);
		var query = new GetUnreadNotificationCountQuery(recipientId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be(7);
	}

	[Test]
	public async Task Handle_ShouldReturnZero_WhenNoUnreadNotificationsExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		_readRepository.CountUnreadByRecipientAsync(recipientId, cancellationToken).Returns(0);
		var query = new GetUnreadNotificationCountQuery(recipientId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be(0);
	}
}
