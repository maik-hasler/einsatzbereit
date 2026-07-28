using Application.Common;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

// Runs after the interceptor (Persistence/Interceptors/ConvertDomainEventsToOutboxMessagesInterceptor.cs)
// has durably captured domain events as outbox rows in the same transaction as the
// triggering command. Dispatching here - in its own scope, well after the triggering
// command's transaction has committed - is what lets an INotificationHandler<T> safely
// call ISender or write to the database (see the "Domain events" section of
// backend/AGENTS.md for the timing problem this replaces).
internal sealed class OutboxProcessorJob(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxProcessorJob> logger)
	: IHostedService, IAsyncDisposable
{
	private const int BatchSize = 20;
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(PollInterval);
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
				await ProcessPendingMessagesAsync(ct).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// A tick-level failure (e.g. a transient SaveChangesAsync error, or the
				// batch query itself losing its connection) must not escape this loop:
				// an unhandled exception here would stop the PeriodicTimer from ever
				// being awaited again, permanently disabling outbox dispatch for the
				// rest of the process's lifetime instead of just this one poll cycle.
				// Log and retry on the next tick - any message left unprocessed is
				// picked up again next time since it still has ProcessedOnUtc == null.
				logger.LogError(ex, "Outbox processor tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task ProcessPendingMessagesAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

		var messages = await dbContext.Set<OutboxMessage>()
			.Where(m => m.ProcessedOnUtc == null)
			.OrderBy(m => m.OccurredOnUtc)
			.Take(BatchSize)
			.ToListAsync(ct);

		foreach (var message in messages)
		{
			try
			{
				var domainEvent = message.ToDomainEvent();
				await dispatcher.DispatchAsync([domainEvent], ct);

				message.ProcessedOnUtc = DateTime.UtcNow;
				message.Error = null;
			}
			catch (Exception ex)
			{
				message.Error = ex.Message;

				logger.LogError(
					ex,
					"Failed to dispatch outbox message {OutboxMessageId} of type {OutboxMessageType}",
					message.Id,
					message.Type);
			}

			await dbContext.SaveChangesAsync(ct);
		}
	}
}
