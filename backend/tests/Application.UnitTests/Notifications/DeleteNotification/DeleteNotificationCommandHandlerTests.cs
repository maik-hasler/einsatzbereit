using Application.Common.Persistence;
using Application.Notifications.DeleteNotification.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.DeleteNotification;

public class DeleteNotificationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Notification, NotificationId> _notificationRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly DeleteNotificationCommandHandler _sut;

	public DeleteNotificationCommandHandlerTests()
	{
		_dbContext.Notifications.Returns(_notificationRepo);
		_sut = new DeleteNotificationCommandHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldDeleteNotificationAndReturnTrue_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new DeleteNotificationCommand(notification.Id, recipientUserId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_notificationRepo.Received(1).Delete(notification);
	}

	[Test]
	public async Task Handle_ShouldReturnFalse_WhenNotificationDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var notificationId = NotificationId.New();
		_notificationRepo.FindAsync(notificationId, cancellationToken).Returns((Notification?)null);
		var command = new DeleteNotificationCommand(notificationId, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeFalse();
		_notificationRepo.DidNotReceiveWithAnyArgs().Delete(default!);
	}

	[Test]
	public async Task Handle_ShouldReturnFalseAndNotDelete_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientUserId = UserId.New();
		var notification = Notification.Create(
			recipientUserId, NotificationKind.EngagementCreated, Guid.NewGuid());
		_notificationRepo.FindAsync(notification.Id, cancellationToken).Returns(notification);
		var command = new DeleteNotificationCommand(notification.Id, Guid.NewGuid());

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert

		result.Should().BeFalse();
		_notificationRepo.DidNotReceiveWithAnyArgs().Delete(default!);
	}
}
