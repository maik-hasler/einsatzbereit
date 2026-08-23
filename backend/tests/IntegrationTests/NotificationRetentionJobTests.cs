using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class NotificationRetentionJobTests(IntegrationTestFixture fixture)
{
	private static readonly DateTimeOffset ReadCutoff = DateTimeOffset.UtcNow.AddDays(-90);
	private static readonly DateTimeOffset UnreadCutoff = DateTimeOffset.UtcNow.AddDays(-180);

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteExpiredNotificationsAsync_ReadNotificationPastReadRetention_IsRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: ReadCutoff.AddDays(-100), readOn: ReadCutoff.AddDays(-1), cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(1);
		await NotificationShouldNotExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_ReadNotificationWithinReadRetention_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: ReadCutoff.AddDays(-100), readOn: ReadCutoff.AddDays(1), cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(0);
		await NotificationShouldExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_ReadNotificationPrunesOnReadDate_NotCreationDate(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: ReadCutoff.AddDays(-100), readOn: DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(0);
		await NotificationShouldExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_UnreadNotificationPastUnreadRetention_IsRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: false, createdOn: UnreadCutoff.AddDays(-1), readOn: null, cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(1);
		await NotificationShouldNotExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_UnreadNotificationPastReadButWithinUnreadRetention_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: false, createdOn: ReadCutoff.AddDays(-1), readOn: null, cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(0);
		await NotificationShouldExistAsync(dbContext, notificationId, cancellationToken);
	}

	private static async Task<NotificationId> SeedNotificationAsync(
		ApplicationDbContext dbContext,
		bool isRead,
		DateTimeOffset createdOn,
		DateTimeOffset? readOn,
		CancellationToken cancellationToken)
	{
		var notification = Notification.Create(
			UserId.Create(Guid.NewGuid()).GetValueOrThrow(),
			NotificationKind.EngagementCreated,
			Guid.NewGuid());

		if (isRead)
			notification.MarkRead(readOn ?? DateTimeOffset.UtcNow);

		dbContext.Set<Notification>().Add(notification);
		await dbContext.SaveChangesAsync(cancellationToken);

		await dbContext.Set<Notification>()
			.Where(n => n.Id == notification.Id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(n => n.CreatedOn, createdOn)
				.SetProperty(n => n.ReadOn, readOn), cancellationToken);

		return notification.Id;
	}

	private static async Task NotificationShouldExistAsync(
		ApplicationDbContext dbContext, NotificationId id, CancellationToken cancellationToken)
	{
		var exists = await dbContext.Set<Notification>()
			.AsNoTracking()
			.AnyAsync(n => n.Id == id, cancellationToken);
		exists.Should().BeTrue();
	}

	private static async Task NotificationShouldNotExistAsync(
		ApplicationDbContext dbContext, NotificationId id, CancellationToken cancellationToken)
	{
		var exists = await dbContext.Set<Notification>()
			.AsNoTracking()
			.AnyAsync(n => n.Id == id, cancellationToken);
		exists.Should().BeFalse();
	}
}
