using Application.Common;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Runs after the interceptor (Persistence/Interceptors/ConvertDomainEventsToOutboxMessagesInterceptor.cs)
// has durably captured domain events as outbox rows in the same transaction as the
// triggering command. Dispatching here - in its own scope, well after the triggering
// command's transaction has committed - is what lets an INotificationHandler<T> safely
// call ISender or write to the database (see the "Domain events" section of
// backend/AGENTS.md for the timing problem this replaces).
internal sealed class OutboxProcessorJob(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxProcessorJob> logger,
	IOptions<OutboxOptions> options,
	OutboxMetrics metrics)
	: IHostedService, IAsyncDisposable
{
	private readonly OutboxOptions _options = options.Value;

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
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

		await ProcessBatchAsync(dbContext, dispatcher, logger, metrics, _options.BatchSize, _options.MaxAttempts, ct);
	}

	// Exposed so IntegrationTests can exercise the row-claiming behavior directly
	// against a real Postgres - e.g. two concurrent calls racing over the same pending
	// batch - without waiting on the real 5s PollInterval from two replicas.
	internal static async Task<int> ProcessBatchAsync(
		ApplicationDbContext dbContext,
		IDomainEventDispatcher dispatcher,
		ILogger logger,
		OutboxMetrics metrics,
		int batchSize,
		int maxAttempts = 5,
		CancellationToken cancellationToken = default)
	{
		// EnableRetryOnFailure (ServiceCollectionExtensions.cs) requires a
		// manually-began transaction to run as one retryable unit via
		// CreateExecutionStrategy() - see
		// ApplicationDbContext.ExecuteInTransactionAsync's comment. A retried
		// attempt re-runs this whole delegate, including re-dispatching
		// messages - the same at-least-once semantics this job already has if
		// the process crashes between a successful DispatchAsync and the
		// commit below, just reachable slightly more often now.
		var strategy = dbContext.Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync<int>(async _ =>
		{
			// FOR UPDATE SKIP LOCKED (#1392): without this, two replicas' timers ticking
			// concurrently would both SELECT the same unprocessed rows and both dispatch
			// them, since nothing previously marked a row "claimed" before dispatch - it was
			// only marked ProcessedOnUtc after dispatch succeeded. Holding the row locks for
			// this whole transaction (not just the SELECT) is what makes a second replica's
			// concurrent SKIP LOCKED query skip these rows entirely instead of blocking on
			// them, so it picks a disjoint batch instead of waiting to double-process this one.
			await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

			var messages = await dbContext.Set<OutboxMessage>()
				.FromSqlInterpolated($@"
					SELECT id, type, content, occurred_on_utc, processed_on_utc, error, attempt_count
					FROM outbox_message
					WHERE processed_on_utc IS NULL
					ORDER BY occurred_on_utc
					LIMIT {batchSize}
					FOR UPDATE SKIP LOCKED")
				.ToListAsync(cancellationToken);

			foreach (var message in messages)
			{
				try
				{
					var domainEvent = message.ToDomainEvent();
					await dispatcher.DispatchAsync([domainEvent], cancellationToken);

					message.ProcessedOnUtc = DateTime.UtcNow;
					message.Error = null;
					metrics.RecordDispatched();
				}
				catch (Exception ex)
				{
					message.Error = ex.Message;
					message.AttemptCount++;
					metrics.RecordFailed();

					logger.LogError(
						ex,
						"Failed to dispatch outbox message {OutboxMessageId} of type {OutboxMessageType} (attempt {AttemptCount}/{MaxAttempts})",
						message.Id,
						message.Type,
						message.AttemptCount,
						maxAttempts);

					if (message.AttemptCount >= maxAttempts)
					{
						// Dead-letter: stamping ProcessedOnUtc stops the WHERE processed_on_utc IS
						// NULL query above from ever re-selecting this row again, so one poison
						// message can no longer stall every message behind it in the batch
						// forever. Error stays populated so this is distinguishable from a
						// genuinely successful dispatch (which clears it). Recorded as its own
						// metric status (not just another "failed") since a dead letter is a
						// terminal give-up an operator should alert on differently than a
						// transient failure that will simply retry next tick (#1008).
						message.ProcessedOnUtc = DateTime.UtcNow;
						metrics.RecordDeadLettered();

						logger.LogError(
							"Outbox message {OutboxMessageId} of type {OutboxMessageType} exceeded {MaxAttempts} attempts and was moved to dead-letter state",
							message.Id,
							message.Type,
							maxAttempts);
					}
				}
			}

			// A single SaveChangesAsync for the whole batch instead of one per message - the
			// row locks from FOR UPDATE above are held for this transaction regardless, so
			// batching the writes costs nothing extra.
			if (messages.Count > 0)
				await dbContext.SaveChangesAsync(cancellationToken);

			await transaction.CommitAsync(cancellationToken);

			// Total backlog remaining after this tick's batch, not just what this tick
			// claimed - lets an operator alert on "outbox.pending" growing unbounded
			// (dispatch is falling behind or stuck) independently of outbox.dispatch's
			// per-attempt succeeded/failed counts (#1008).
			var pendingCount = await dbContext.Set<OutboxMessage>()
				.AsNoTracking()
				.LongCountAsync(m => m.ProcessedOnUtc == null, cancellationToken);
			metrics.RecordPending(pendingCount);

			return messages.Count;
		}, cancellationToken);
	}
}
