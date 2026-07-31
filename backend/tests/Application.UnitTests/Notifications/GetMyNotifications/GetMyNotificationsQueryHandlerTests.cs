using Application.Notifications;
using Application.Notifications.GetMyNotifications.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Notifications.GetMyNotifications;

public class GetMyNotificationsQueryHandlerTests
{
	private readonly INotificationReadRepository _readRepository = Substitute.For<INotificationReadRepository>();
	private readonly GetMyNotificationsQueryHandler _sut;

	public GetMyNotificationsQueryHandlerTests()
	{
		_sut = new GetMyNotificationsQueryHandler(_readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnAllItemsAndHasMoreFalse_WhenFewerThanPageSizeExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		var notifications = CreateSummaries(10);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Items.Should().HaveCount(10);
		result.HasMore.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnExactlyPageSizeItemsAndHasMoreFalse_WhenExactlyPageSizeExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		var notifications = CreateSummaries(50);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Items.Should().HaveCount(50);
		result.HasMore.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnOnlyPageSizeItemsAndHasMoreTrue_WhenMoreThanPageSizeExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		var notifications = CreateSummaries(51);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Items.Should().HaveCount(50);
		result.HasMore.Should().BeTrue();
		result.Items.Should().BeEquivalentTo(notifications.Take(50), o => o.WithStrictOrdering());
	}

	[Test]
	public async Task Handle_ShouldForwardBeforeCursorToRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var recipientId = UserId.New();
		var before = DateTimeOffset.UtcNow.AddDays(-1);
		var beforeId = Guid.NewGuid();
		_readRepository.GetByRecipientAsync(recipientId, before, beforeId, 51, cancellationToken)
			.Returns([]);
		var query = new GetMyNotificationsQuery(recipientId, before, beforeId);

		// Act
		await _sut.Handle(query, cancellationToken);

		// Assert
		await _readRepository.Received(1).GetByRecipientAsync(recipientId, before, beforeId, 51, cancellationToken);
	}

	private static List<NotificationSummary> CreateSummaries(int count) =>
		Enumerable.Range(0, count)
			.Select(i => new NotificationSummary(
				Guid.NewGuid(),
				"EngagementCreated",
				"Some Title",
				"/my-engagements",
				false,
				DateTimeOffset.UtcNow.AddMinutes(-i)))
			.ToList();
}
