using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Nothing else ever deletes a processed outbox_message row, so the table grew
// without bound - this periodically prunes rows once they are both processed and
// past RetentionDays old (#1144). Deliberately leaves dead-lettered rows (MaxAttempts
// exhausted in OutboxProcessorJob - ProcessedOnUtc stamped like a success, but Error
// stays populated to distinguish it) untouched - those are the only record that
// something went wrong and stay available for inspection.
internal sealed class OutboxRetentionJob(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxRetentionJob> logger,
	IOptions<OutboxOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly OutboxOptions _options = options.Value;

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
				// A transient failure here just means the same rows are still there to
				// prune on the next tick - never worth crashing the whole process over.
				logger.LogError(ex, "Outbox retention tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var deleted = await DeleteExpiredProcessedMessagesAsync(
			dbContext, DateTime.UtcNow.AddDays(-_options.RetentionDays), ct);

		if (deleted > 0)
			logger.LogInformation("Pruned {Count} processed outbox message(s)", deleted);
	}

	// Exposed so IntegrationTests can exercise the deletion directly against a real
	// Postgres without waiting on the real RetentionCheckIntervalHours.
	internal static async Task<int> DeleteExpiredProcessedMessagesAsync(
		ApplicationDbContext dbContext,
		DateTime cutoffUtc,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<OutboxMessage>()
			.Where(m => m.ProcessedOnUtc != null && m.Error == null && m.ProcessedOnUtc < cutoffUtc)
			.ExecuteDeleteAsync(cancellationToken);
}
