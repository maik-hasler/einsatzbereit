using Domain.Reports;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

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

	internal static async Task<int> DeleteExpiredReportsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset targetDeletedCutoff,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<Report>()
			.Where(r => r.TargetDeletedOn != null && r.TargetDeletedOn < targetDeletedCutoff)
			.ExecuteDeleteAsync(cancellationToken);
}
