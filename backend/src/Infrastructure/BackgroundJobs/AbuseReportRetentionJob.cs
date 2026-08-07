using Domain.Reports;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Reports where the deleted user is the *target* survive DeleteMyAccountCommandHandler
// as moderation history (only reports the deleted user *filed*, as reporter, are
// hard-deleted immediately - DeleteReportsForReporterAsync) and had no retention limit
// at all, outliving the account they concern indefinitely with no disclosure in the
// privacy policy. This periodically prunes them, measured from Report.TargetDeletedOn
// (stamped once, when the target account is deleted), not from CreatedOn (#1725).
internal sealed class AbuseReportRetentionJob(
	IServiceScopeFactory scopeFactory,
	ILogger<AbuseReportRetentionJob> logger,
	IOptions<AbuseReportRetentionOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly AbuseReportRetentionOptions _options = options.Value;

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
				logger.LogError(ex, "Abuse report retention tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var now = DateTimeOffset.UtcNow;
		var deleted = await DeleteExpiredReportsAsync(
			dbContext,
			targetDeletedCutoff: now.AddDays(-_options.RetentionDaysAfterTargetDeleted),
			ct);

		if (deleted > 0)
			logger.LogInformation("Pruned {Count} expired abuse report(s)", deleted);
	}

	// Exposed so IntegrationTests can exercise the deletion directly against a real
	// Postgres without waiting on the real RetentionCheckIntervalHours.
	internal static async Task<int> DeleteExpiredReportsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset targetDeletedCutoff,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<Report>()
			.Where(r => r.TargetDeletedOn != null && r.TargetDeletedOn < targetDeletedCutoff)
			.ExecuteDeleteAsync(cancellationToken);
}
