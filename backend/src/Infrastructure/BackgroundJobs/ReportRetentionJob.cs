using Domain.Reports;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Report rows had no automatic cleanup path at all (einsatzbereit#1725):
// DeleteMyAccountCommandHandler only deletes reports the deleted user filed as
// reporter (DeleteReportsForReporterAsync), never reports filed against them,
// and AdminShadowDeleteUserCommandHandler only auto-resolves (MarkActioned)
// open reports against a shadow-deleted user without ever deleting the row -
// so reports accumulated forever with no disclosed retention period. This job
// closes both gaps: any resolved report is pruned once it's older than
// ResolvedRetentionDays, and a report naming a user as its target is pruned
// immediately once that user's row is gone entirely (a hard self-deletion via
// DeleteMyAccountCommandHandler) - a shadow-deleted user's row still
// physically exists, so their reports are left alone by that second rule
// until either they age out via the first rule or the account is later fully
// erased.
internal sealed class ReportRetentionJob(
	IServiceScopeFactory scopeFactory,
	ILogger<ReportRetentionJob> logger,
	IOptions<ReportRetentionOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly ReportRetentionOptions _options = options.Value;

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
				logger.LogError(ex, "Report retention tick failed; will retry on the next poll interval");
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
			resolvedCutoff: now.AddDays(-_options.ResolvedRetentionDays),
			ct);

		if (deleted > 0)
			logger.LogInformation("Pruned {Count} expired/orphaned report(s)", deleted);
	}

	// Exposed so IntegrationTests can exercise the deletion directly against a real
	// Postgres without waiting on the real RetentionCheckIntervalHours.
	internal static async Task<int> DeleteExpiredReportsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset resolvedCutoff,
		CancellationToken cancellationToken = default)
	{
		var resolvedExpired = await dbContext.Set<Report>()
			.Where(r => r.Status != ReportStatus.Open && r.ResolvedOn != null && r.ResolvedOn < resolvedCutoff)
			.ExecuteDeleteAsync(cancellationToken);

		var orphanedTargetDeleted = await dbContext.DeleteReportsTargetingNonExistentUsersAsync(cancellationToken);

		return resolvedExpired + orphanedTargetDeleted;
	}
}
