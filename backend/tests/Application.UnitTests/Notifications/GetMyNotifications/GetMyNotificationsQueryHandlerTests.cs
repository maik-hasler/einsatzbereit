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
		var recipientId = UserId.New();
		var notifications = CreateSummaries(10);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().HaveCount(10);
		result.HasMore.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnExactlyPageSizeItemsAndHasMoreFalse_WhenExactlyPageSizeExist(
		CancellationToken cancellationToken)
	{
		var recipientId = UserId.New();
		var notifications = CreateSummaries(50);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().HaveCount(50);
		result.HasMore.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldReturnOnlyPageSizeItemsAndHasMoreTrue_WhenMoreThanPageSizeExist(
		CancellationToken cancellationToken)
	{
		var recipientId = UserId.New();
		var notifications = CreateSummaries(51);
		_readRepository.GetByRecipientAsync(recipientId, null, null, 51, cancellationToken)
			.Returns(notifications);
		var query = new GetMyNotificationsQuery(recipientId, null, null);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().HaveCount(50);
		result.HasMore.Should().BeTrue();
		result.Items.Should().BeEquivalentTo(notifications.Take(50), o => o.WithStrictOrdering());
	}

	[Test]
	public async Task Handle_ShouldForwardBeforeCursorToRepository(
		CancellationToken cancellationToken)
	{
		var recipientId = UserId.New();
		var before = DateTimeOffset.UtcNow.AddDays(-1);
		var beforeId = Guid.NewGuid();
		_readRepository.GetByRecipientAsync(recipientId, before, beforeId, 51, cancellationToken)
			.Returns([]);
		var query = new GetMyNotificationsQuery(recipientId, before, beforeId);

		await _sut.Handle(query, cancellationToken);

		await _readRepository.Received(1).GetByRecipientAsync(recipientId, before, beforeId, 51, cancellationToken);
	}

	private static List<NotificationSummary> CreateSummaries(int count) =>
		Enumerable.Range(0, count)
			.Select(i => new NotificationSummary(
				Guid.NewGuid(),
				"EngagementCreated",
				"Some Title",
				"/my-signups",
				false,
				DateTimeOffset.UtcNow.AddMinutes(-i)))
			.ToList();
}
