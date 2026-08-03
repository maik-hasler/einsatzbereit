using Application.Common.Persistence;
using Application.Notifications.MarkAllNotificationsRead.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly MarkAllNotificationsReadCommandHandler _sut;

	public MarkAllNotificationsReadCommandHandlerTests()
	{
		_sut = new MarkAllNotificationsReadCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldMarkEveryUnreadNotificationAsRead_AndReturnTheCount(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		var unread = new List<Notification>
		{
			Notification.Create(recipientId, NotificationKind.EngagementCreated, Guid.NewGuid()),
			Notification.Create(recipientId, NotificationKind.EngagementConfirmed, Guid.NewGuid()),
			Notification.Create(recipientId, NotificationKind.InvitationReceived, Guid.NewGuid()),
		};
		_dbContext.GetUnreadNotificationsForRecipientAsync(recipientId, cancellationToken).Returns(unread);

		var command = new MarkAllNotificationsReadCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(3);
		unread.Should().OnlyContain(n => n.IsRead);
	}

	[Test]
	public async Task Handle_ShouldReturnZero_WhenCallerHasNoUnreadNotifications(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		_dbContext.GetUnreadNotificationsForRecipientAsync(recipientId, cancellationToken)
			.Returns([]);

		var command = new MarkAllNotificationsReadCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(0);
	}

	[Test]
	public async Task Handle_ShouldOnlyAffectTheRequestingUsersOwnNotifications(
		CancellationToken cancellationToken)
	{
		// Arrange: the repository query itself is scoped to recipientId - this
		// asserts the handler passes the command's own RecipientId through
		// rather than some other identity, so a caller can never mark another
		// user's notifications as read.
		var recipientId = UserId.New();
		var otherUsersId = UserId.New();
		_dbContext.GetUnreadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>())
			.Returns([Notification.Create(otherUsersId, NotificationKind.EngagementCreated, Guid.NewGuid())]);
		_dbContext.GetUnreadNotificationsForRecipientAsync(recipientId, cancellationToken)
			.Returns([]);

		var command = new MarkAllNotificationsReadCommand(recipientId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().Be(0);
		await _dbContext.DidNotReceive().GetUnreadNotificationsForRecipientAsync(otherUsersId, Arg.Any<CancellationToken>());
	}
}
