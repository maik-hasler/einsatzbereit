using Application.Common.Persistence;
using Application.Notifications.MarkNotificationRead.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.MarkNotificationRead;

public class MarkNotificationReadCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Notification, NotificationId> _notificationRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly MarkNotificationReadCommandHandler _sut;

	public MarkNotificationReadCommandHandlerTests()
	{
		_dbContext.Notifications.Returns(_notificationRepo);
		_sut = new MarkNotificationReadCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldMarkNotificationReadAndReturnTrue_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationReadCommand(notification.Id, recipientUserId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		notification.IsRead.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldReturnFalse_WhenNotificationDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var notificationId = NotificationId.New();
		_notificationRepo.FindAsync(notificationId, cancellationToken).Returns((Notification?)null);
		var command = new MarkNotificationReadCommand(notificationId, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnFalseAndNotMarkRead_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationReadCommand(notification.Id, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		// A cross-user attempt collapses into the same "false" result as a nonexistent id,
		// deliberately not leaking whether the id belongs to someone else - this is exactly
		// the ownership-verification branch GitHub issue #829 asks to cover directly.
		result.Should().BeFalse();
		notification.IsRead.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenNotificationIsAlreadyRead(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		notification.MarkRead();
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationReadCommand(notification.Id, recipientUserId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		notification.IsRead.Should().BeTrue();
	}
}
