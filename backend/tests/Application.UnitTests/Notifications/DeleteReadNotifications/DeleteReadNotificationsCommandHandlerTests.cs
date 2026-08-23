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
		var recipientId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken).Returns(3);

		var command = new DeleteReadNotificationsCommand(recipientId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().Be(3);
	}

	[Test]
	public async Task Handle_ShouldReturnZero_WhenCallerHasNoReadNotifications(
		CancellationToken cancellationToken)
	{
		var recipientId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken).Returns(0);

		var command = new DeleteReadNotificationsCommand(recipientId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().Be(0);
	}

	[Test]
	public async Task Handle_ShouldOnlyAffectTheRequestingUsersOwnNotifications(
		CancellationToken cancellationToken)
	{
		var recipientId = UserId.New();
		var otherUsersId = UserId.New();
		_dbContext.DeleteReadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>())
			.Returns(5);
		_dbContext.DeleteReadNotificationsForRecipientAsync(recipientId, cancellationToken)
			.Returns(0);

		var command = new DeleteReadNotificationsCommand(recipientId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().Be(0);
		await _dbContext.DidNotReceive().DeleteReadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>());
	}
}
