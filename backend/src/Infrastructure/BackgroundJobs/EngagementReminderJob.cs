using Domain.Engagements;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Detects which confirmed engagements need a 24h reminder and atomically claims +
// queues one EngagementReminderDueDomainEvent per engagement into the outbox (#1392) -
// it no longer sends anything itself. Actual delivery is handled the same way as every
// other domain event, by OutboxProcessorJob dispatching to
// Application.Engagements.EngagementReminder.v1.EngagementReminderDueHandler, so a
// second replica running this job concurrently can never double-send a reminder: the
// claim below is a single atomic UPDATE per candidate, not a racy read-then-write.
internal sealed class EngagementReminderJob(
	IServiceScopeFactory scopeFactory,
	ILogger<EngagementReminderJob> logger,
	IOptions<EngagementReminderOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly EngagementReminderOptions _options = options.Value;

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
				// Mirrors OutboxProcessorJob's tick-level catch: a failure here (e.g. a
				// transient DB error) must not stop the PeriodicTimer from ever being
				// awaited again. Any due engagement still has ReminderSentAt == null,
				// so it is simply picked up again on the next tick.
				logger.LogError(ex, "Engagement reminder tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var queued = await ClaimAndQueueRemindersAsync(dbContext, DateTimeOffset.UtcNow, _options.MaxBatchSize, ct);

		if (queued > 0)
			logger.LogInformation("Queued {Count} reminder(s) for outbox dispatch", queued);
	}

	// Exposed so IntegrationTests can exercise the claim race directly against a real
	// ApplicationDbContext - e.g. two concurrent calls racing over the same due
	// engagement, without waiting an hour for a real tick from two replicas.
	internal static async Task<int> ClaimAndQueueRemindersAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset now,
		int maxBatchSize,
		CancellationToken cancellationToken = default)
	{
		var windowStart = now.AddHours(23);
		var windowEnd = now.AddHours(25);

		// One transaction for the whole claim-and-enqueue batch: without it, a crash or
		// a failed SaveChangesAsync after some ExecuteUpdateAsync claims already
		// auto-committed would leave those engagements with ReminderSentAt permanently
		// set but no outbox row ever written for them - silently losing the reminder
		// forever instead of retrying it next tick. Wrapping in a transaction makes the
		// whole batch all-or-nothing while still preserving the per-row claim below:
		// Postgres still evaluates each UPDATE's WHERE clause atomically against
		// whatever the row's current committed state is.
		await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

		// Caps how many engagements one tick claims. Anything left over still has
		// ReminderSentAt == null and its TimeSlot still falls in the (now+23h, now+25h)
		// window on the next tick (the window is 2h wide, the timer fires every
		// PollIntervalHours), so it is picked up then instead of being lost.
		var candidates = await dbContext.Set<Engagement>()
			.Where(e =>
				e.Status == EngagementStatus.Confirmed &&
				e.TimeSlotId != null &&
				e.ReminderSentAt == null)
			.Join(
				dbContext.Set<TimeSlot>(),
				e => e.TimeSlotId,
				ts => ts.Id,
				(e, ts) => new { e.Id, e.VolunteerId, e.OpportunityId, e.TimeSlotId, ts.StartDateTime })
			.Where(x => x.StartDateTime >= windowStart && x.StartDateTime <= windowEnd)
			.OrderBy(x => x.StartDateTime)
			.Take(maxBatchSize)
			.ToListAsync(cancellationToken);

		if (candidates.Count == 0)
		{
			await transaction.CommitAsync(cancellationToken);
			return 0;
		}

		var occurredOnUtc = now.UtcDateTime;
		var claimedMessages = new List<OutboxMessage>(candidates.Count);

		foreach (var candidate in candidates)
		{
			// Atomic per-row claim: this UPDATE's WHERE clause is re-evaluated by
			// Postgres against the row's current committed state, so if another
			// replica's tick already claimed this engagement, it affects 0 rows instead
			// of racing to a duplicate reminder (#1392) - unlike the plain
			// tracked-entity SaveChangesAsync this job used to do, which had no guard
			// against exactly that race.
			var rowsAffected = await dbContext.Set<Engagement>()
				.Where(e => e.Id == candidate.Id && e.ReminderSentAt == null)
				.ExecuteUpdateAsync(
					s => s
						.SetProperty(e => e.ReminderSentAt, now)
						.SetProperty(e => e.ModifiedOn, now),
					cancellationToken);

			if (rowsAffected == 0)
				continue;

			var domainEvent = new EngagementReminderDueDomainEvent(
				candidate.Id, candidate.VolunteerId!.Value, candidate.OpportunityId, candidate.TimeSlotId!.Value);
			claimedMessages.Add(OutboxMessage.FromDomainEvent(domainEvent, occurredOnUtc));
		}

		if (claimedMessages.Count == 0)
		{
			await transaction.CommitAsync(cancellationToken);
			return 0;
		}

		// ExecuteUpdateAsync above bypasses the ChangeTracker, so
		// ConvertDomainEventsToOutboxMessagesInterceptor never sees these events - the
		// outbox rows are built directly here instead, then written in a single batched
		// SaveChangesAsync for however many this tick actually won.
		dbContext.Set<OutboxMessage>().AddRange(claimedMessages);
		await dbContext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return claimedMessages.Count;
	}
}
