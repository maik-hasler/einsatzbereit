using Application.Common;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

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
				// Deliberately CancellationToken.None, not ct: once a batch is claimed it
				// must dispatch and persist every message before stopping, even if shutdown
				// is requested mid-tick - otherwise an already-dispatched message whose
				// processed/backoff write hasn't landed yet gets reclaimed and re-dispatched
				// once its claim expires. The host's shutdown timeout still bounds
				// how long StopAsync waits for this to finish.
				await ProcessPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);
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

		await ProcessBatchAsync(
			dbContext, dispatcher, logger, metrics, _options.BatchSize, _options.MaxAttempts, _options.ClaimTimeoutSeconds,
			_options.RetryBackoffBaseSeconds, ct);
	}

	internal static async Task<int> ProcessBatchAsync(
		ApplicationDbContext dbContext,
		IDomainEventDispatcher dispatcher,
		ILogger logger,
		OutboxMetrics metrics,
		int batchSize,
		int maxAttempts = 5,
		int claimTimeoutSeconds = 300,
		int retryBackoffBaseSeconds = 300,
		CancellationToken cancellationToken = default)
	{
		var messages = await ClaimBatchAsync(dbContext, batchSize, claimTimeoutSeconds, cancellationToken);

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

				message.ClaimedOnUtc = null;

				logger.LogError(
					ex,
					"Failed to dispatch outbox message {OutboxMessageId} of type {OutboxMessageType} (attempt {AttemptCount}/{MaxAttempts})",
					message.Id,
					message.Type,
					message.AttemptCount,
					maxAttempts);

				if (message.AttemptCount >= maxAttempts)
				{
					message.ProcessedOnUtc = DateTime.UtcNow;
					metrics.RecordDeadLettered();

					logger.LogError(
						"Outbox message {OutboxMessageId} of type {OutboxMessageType} exceeded {MaxAttempts} attempts and was moved to dead-letter state",
						message.Id,
						message.Type,
						maxAttempts);
				}
				else
				{
					// Exponential backoff instead of an immediate retry: a brief
					// dependency outage (e.g. Keycloak restarting) must not exhaust the
					// whole retry budget in the ~25s a fixed poll interval would otherwise allow.
					var backoffSeconds = Math.Pow(2, message.AttemptCount) * retryBackoffBaseSeconds;
					message.NextAttemptOnUtc = DateTime.UtcNow.AddSeconds(backoffSeconds);
				}
			}

			// Persisted per message rather than batched after the loop: a mid-batch crash
			// or SIGTERM must not lose the processed/backoff state of messages already
			// dispatched earlier in this same batch, which would otherwise have their
			// side effects (e.g. a sent email) repeated on redelivery.
			await dbContext.SaveChangesAsync(cancellationToken);
		}

		var pendingCount = await dbContext.Set<OutboxMessage>()
			.AsNoTracking()
			.LongCountAsync(m => m.ProcessedOnUtc == null, cancellationToken);
		metrics.RecordPending(pendingCount);

		return messages.Count;
	}

	private static async Task<List<OutboxMessage>> ClaimBatchAsync(
		ApplicationDbContext dbContext,
		int batchSize,
		int claimTimeoutSeconds,
		CancellationToken cancellationToken)
	{
		var strategy = dbContext.Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync(async _ =>
		{
			// FOR UPDATE SKIP LOCKED (#1392): without this, two replicas' timers ticking
			// concurrently would both SELECT the same unprocessed rows and both dispatch
			// them. Held only for this claim+stamp transaction (not across dispatch) -
			// a second replica's concurrent SKIP LOCKED query skips these rows entirely
			// instead of blocking on them, so it picks a disjoint batch instead of
			// waiting to double-process this one.
			await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

			var now = DateTime.UtcNow;
			var staleCutoff = now.AddSeconds(-claimTimeoutSeconds);

			var messages = await dbContext.Set<OutboxMessage>()
				.FromSqlInterpolated($@"
					SELECT id, type, content, occurred_on_utc, processed_on_utc, error, attempt_count, claimed_on_utc, next_attempt_on_utc
					FROM outbox_message
					WHERE processed_on_utc IS NULL
						AND (claimed_on_utc IS NULL OR claimed_on_utc <= {staleCutoff})
						AND (next_attempt_on_utc IS NULL OR next_attempt_on_utc <= {now})
					ORDER BY occurred_on_utc
					LIMIT {batchSize}
					FOR UPDATE SKIP LOCKED")
				.ToListAsync(cancellationToken);

			var claimedOnUtc = DateTime.UtcNow;
			foreach (var message in messages)
				message.ClaimedOnUtc = claimedOnUtc;

			if (messages.Count > 0)
				await dbContext.SaveChangesAsync(cancellationToken);

			await transaction.CommitAsync(cancellationToken);

			return messages;
		}, cancellationToken);
	}
}
