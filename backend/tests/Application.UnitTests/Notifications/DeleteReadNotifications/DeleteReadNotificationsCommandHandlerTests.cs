using Application.Common.Persistence;
using Application.Notifications.DeleteReadNotifications.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.DeleteReadNotifications;

public class DeleteReadNotificationsCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly DeleteReadNotificationsCommandHandler _sut;

	public DeleteReadNotificationsCommandHandlerTests()
	{
		_sut = new DeleteReadNotificationsCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldReturnTheDeletedCount_FromTheDbContext(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken).Returns(3);

		var command = new DeleteReadNotificationsCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(3);
	}

	[Test]
	public async Task Handle_ShouldReturnZero_WhenCallerHasNoReadNotifications(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken).Returns(0);

		var command = new DeleteReadNotificationsCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(0);
	}

	[Test]
	public async Task Handle_ShouldOnlyAffectTheRequestingUsersOwnNotifications(
		CancellationToken cancellationToken)
	{
		// Arrange: the dbContext call itself is scoped to recipientId - this
		// asserts the handler passes the command's own RecipientId through
		// rather than some other identity, so a caller can never delete
		// another user's notifications.
		var recipientId = UserId.New();
		var otherUsersId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>())
			.Returns(5);
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken)
			.Returns(0);

		var command = new DeleteReadNotificationsCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(0);
		await _dbContext.DidNotReceive().DeleteReadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>());
	}
}
