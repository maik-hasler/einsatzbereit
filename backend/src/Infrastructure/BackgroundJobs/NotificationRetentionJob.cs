using Domain.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

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

	internal static async Task<int> DeleteExpiredNotificationsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset readCutoff,
		DateTimeOffset unreadCutoff,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<Notification>()
			.Where(n =>
				(n.IsRead && n.ReadOn != null && n.ReadOn < readCutoff) ||
				(!n.IsRead && n.CreatedOn < unreadCutoff))
			.ExecuteDeleteAsync(cancellationToken);
}
