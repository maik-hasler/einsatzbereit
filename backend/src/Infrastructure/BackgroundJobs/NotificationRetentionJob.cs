using Domain.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Notification.RelatedEntityId is an untyped uuid pointing at an engagement, an
// opportunity, or an invitation with no FK - a deleted target just leaves the
// notification pointing at nothing (NotificationReadRepository already
// tolerates this, falling back to a placeholder). Nothing else ever deletes a
// notification row (aside from DeleteMyAccountCommandHandler wiping a whole
// recipient's rows), so the table grew without bound - this periodically
// prunes read rows past ReadRetentionDays, and unread rows (which could
// otherwise keep pointing at a long-deleted target indefinitely until the
// recipient happens to open them) past the longer UnreadRetentionDays (#1209).
internal sealed class NotificationRetentionJob(
	IServiceScopeFactory scopeFactory,
	ILogger<NotificationRetentionJob> logger,
	IOptions<NotificationRetentionOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly NotificationRetentionOptions _options = options.Value;

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromHours(_options.RetentionCheckIntervalHours));
		_executeTask = RunLoopAsync(_cts.Token);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_cts is not null)
			await _cts.CancelAsync();

		try
		{
			await _executeTask.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ValueTask DisposeAsync()
	{
		_timer?.Dispose();
		_cts?.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task RunLoopAsync(CancellationToken ct)
	{
		if (_timer is null) return;

		while (!ct.IsCancellationRequested && await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
		{
			try
			{
				await TickAsync(ct).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// A row that should have been pruned this tick just gets picked up
				// again on the next one - no data is lost by skipping a tick.
				logger.LogError(ex, "Notification retention tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var now = DateTimeOffset.UtcNow;
		var deleted = await DeleteExpiredNotificationsAsync(
			dbContext,
			readCutoff: now.AddDays(-_options.ReadRetentionDays),
			unreadCutoff: now.AddDays(-_options.UnreadRetentionDays),
			ct);

		if (deleted > 0)
			logger.LogInformation("Pruned {Count} expired notification(s)", deleted);
	}

	// Exposed so IntegrationTests can exercise the deletion directly against a real
	// Postgres without waiting on the real RetentionCheckIntervalHours.
	internal static async Task<int> DeleteExpiredNotificationsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset readCutoff,
		DateTimeOffset unreadCutoff,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<Notification>()
			.Where(n =>
				// #1725: the read branch used to key off CreatedOn, deleting a
				// notification read a day after creation almost immediately
				// instead of ReadRetentionDays after it was actually read.
				(n.IsRead && n.ReadOn != null && n.ReadOn < readCutoff) ||
				(!n.IsRead && n.CreatedOn < unreadCutoff))
			.ExecuteDeleteAsync(cancellationToken);
}
