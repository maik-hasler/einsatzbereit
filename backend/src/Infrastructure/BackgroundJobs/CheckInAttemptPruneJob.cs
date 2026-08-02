using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Infrastructure.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// CheckInAttempt has no other cleanup path (#1176) - without this job the
// table would grow for the lifetime of the database, one row per engagement
// that ever had a failed check-in PIN attempt. A row is safe to drop once its
// lockout window has fully elapsed: RegisterFailedAttemptAsync only writes a
// new row if none exists, so pruning gives that engagement a fresh
// FailedAttempts count on its next wrong guess - the same reset a legitimate
// owner already gets immediately by entering the correct PIN (ResetAsync).
internal sealed class CheckInAttemptPruneJob(
	IServiceScopeFactory scopeFactory,
	ILogger<CheckInAttemptPruneJob> logger,
	IOptions<CheckInAttemptPruneOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly CheckInAttemptPruneOptions _options = options.Value;

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromHours(_options.PollIntervalHours));
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
				logger.LogError(ex, "Check-in attempt prune tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var pruned = await PruneExpiredAttemptsAsync(dbContext, DateTimeOffset.UtcNow, ct);

		if (pruned > 0)
			logger.LogInformation("Pruned {Count} expired check-in attempt record(s)", pruned);
	}

	// Exposed so IntegrationTests can exercise pruning directly against a real
	// ApplicationDbContext instead of waiting for a real tick.
	internal static async Task<int> PruneExpiredAttemptsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset now,
		CancellationToken cancellationToken = default)
	{
		var cutoff = now - CheckInAttemptLimiter.LockoutDuration;

		return await dbContext.Set<CheckInAttempt>()
			.Where(a => a.LastAttemptOn < cutoff)
			.ExecuteDeleteAsync(cancellationToken);
	}
}
