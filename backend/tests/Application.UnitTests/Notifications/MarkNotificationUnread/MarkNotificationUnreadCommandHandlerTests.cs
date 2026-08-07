using Application.Common.Persistence;
using Application.Notifications.MarkNotificationUnread.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.MarkNotificationUnread;

public class MarkNotificationUnreadCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Notification, NotificationId> _notificationRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly MarkNotificationUnreadCommandHandler _sut;

	public MarkNotificationUnreadCommandHandlerTests()
	{
		_dbContext.Notifications.Returns(_notificationRepo);
		_sut = new MarkNotificationUnreadCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldMarkNotificationUnreadAndReturnTrue_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		notification.MarkRead(DateTimeOffset.UtcNow);
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationUnreadCommand(notification.Id, recipientUserId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		notification.IsRead.Should().BeFalse();
		notification.ReadOn.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldReturnFalse_WhenNotificationDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var notificationId = NotificationId.New();
		_notificationRepo.FindAsync(notificationId, cancellationToken).Returns((Notification?)null);
		var command = new MarkNotificationUnreadCommand(notificationId, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnFalseAndNotMarkUnread_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		notification.MarkRead(DateTimeOffset.UtcNow);
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationUnreadCommand(notification.Id, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		// Mirrors MarkNotificationRead's ownership check (einsatzbereit#829): a
		// cross-user attempt collapses into the same "false" result as a
		// nonexistent id rather than leaking whether the id belongs to someone else.
		result.Should().BeFalse();
		notification.IsRead.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenNotificationIsAlreadyUnread(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new MarkNotificationUnreadCommand(notification.Id, recipientUserId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		notification.IsRead.Should().BeFalse();
	}
}
