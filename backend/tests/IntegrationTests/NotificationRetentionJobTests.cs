using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Exercises Infrastructure.BackgroundJobs.NotificationRetentionJob.DeleteExpiredNotificationsAsync
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real integration
// Postgres, rather than waiting a real 24-hour tick for the pruning behavior (#1209) to
// become observable.
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
		// #1725: read branch keys off ReadOn, not CreatedOn - created long ago but
		// only just past the read cutoff.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: ReadCutoff.AddDays(-30), readOn: ReadCutoff.AddDays(-1), cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(1);
		await NotificationShouldNotExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_ReadNotificationWithinReadRetention_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		// #1725: created long before the read cutoff, but only read recently -
		// the old CreatedOn-based bug would have deleted this immediately.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: ReadCutoff.AddDays(-30), readOn: ReadCutoff.AddDays(1), cancellationToken);

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
		// Regression guard for the read/unread split itself: an unread
		// notification older than the (shorter) read retention window must
		// survive until it also crosses the longer unread retention window.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: false, createdOn: ReadCutoff.AddDays(-1), readOn: null, cancellationToken);

		var deleted = await NotificationRetentionJob.DeleteExpiredNotificationsAsync(
			dbContext, ReadCutoff, UnreadCutoff, cancellationToken);

		deleted.Should().Be(0);
		await NotificationShouldExistAsync(dbContext, notificationId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredNotificationsAsync_ReadNotificationWithNoReadOnStamp_IsNeverRemoved(
		CancellationToken cancellationToken)
	{
		// Defensive guard (#1725): a row that is somehow marked read without a
		// ReadOn stamp (e.g. a pre-migration legacy row the backfill missed)
		// must not match either branch and be silently deleted with no
		// evidence of when it was actually read.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var notificationId = await SeedNotificationAsync(
			dbContext, isRead: true, createdOn: UnreadCutoff.AddDays(-1), readOn: null, cancellationToken);

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
			notification.MarkRead(readOn ?? createdOn);

		dbContext.Set<Notification>().Add(notification);
		await dbContext.SaveChangesAsync(cancellationToken);

		// CreatedOn is stamped by AuditableEntityInterceptor on save - overwrite
		// it directly afterward so seeded rows can simulate an arbitrary age.
		// ReadOn isn't IAuditableEntity-tracked, so MarkRead's value above
		// persists as-is; but the "isRead without a ReadOn stamp" case needs a
		// direct override since MarkRead(DateTimeOffset) always sets it.
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
