using Domain.SearchAlerts;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Periodically scans for opportunities published since each active SearchAlert's last
// check and queues one SearchAlertMatchesFoundDomainEvent per alert with at least one
// match into the outbox (#1090) - it doesn't send anything itself. Actual delivery is
// handled the same way as every other domain event, by OutboxProcessorJob dispatching
// to Application.Users.SearchAlertDigest.v1.SearchAlertMatchesFoundNotificationHandler,
// so a second replica running this job concurrently can never double-send a digest: the
// claim below is a single atomic UPDATE per alert, not a racy read-then-write. Runs once
// a day rather than reacting to VolunteerOpportunityPublishedDomainEvent directly, so a
// volunteer gets one digest of everything new instead of a separate email per opportunity
// whenever an organizer bulk-publishes a series.
internal sealed class SearchAlertDigestJob(
	IServiceScopeFactory scopeFactory,
	ILogger<SearchAlertDigestJob> logger,
	IOptions<SearchAlertDigestOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly SearchAlertDigestOptions _options = options.Value;

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
				// Mirrors OutboxProcessorJob/EngagementReminderJob's tick-level catch: a
				// failure here (e.g. a transient DB error) must not stop the
				// PeriodicTimer from ever being awaited again. Every alert still has
				// its old LastNotifiedAt, so it is simply picked up again on the next
				// tick.
				logger.LogError(ex, "Search alert digest tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var queued = await ClaimAndQueueMatchesAsync(dbContext, DateTimeOffset.UtcNow, _options.MaxBatchSize, ct);

		if (queued > 0)
			logger.LogInformation("Queued {Count} search alert digest(s) for outbox dispatch", queued);
	}

	// Exposed so IntegrationTests can exercise the claim race directly against a real
	// ApplicationDbContext - e.g. two concurrent calls racing over the same due
	// alert, without waiting a day for a real tick from two replicas.
	internal static async Task<int> ClaimAndQueueMatchesAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset now,
		int maxBatchSize,
		CancellationToken cancellationToken = default)
	{
		var strategy = dbContext.Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync<int>(async _ =>
		{
			// One transaction for the whole claim-and-enqueue batch - see
			// EngagementReminderJob's identical comment for why: without it, a crash
			// after some ExecuteUpdateAsync claims already auto-committed but before
			// SaveChangesAsync would leave those alerts with an advanced
			// LastNotifiedAt but no outbox row ever written for them, silently
			// losing the digest forever instead of retrying it next tick.
			await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

			// Least-recently-checked alerts first, so if there are ever more active
			// alerts than one tick's batch size, every alert still gets a turn across
			// consecutive ticks instead of the same head of the table starving the
			// rest.
			var alerts = await dbContext.Set<SearchAlert>()
				.AsNoTracking()
				.OrderBy(s => s.LastNotifiedAt)
				.Take(maxBatchSize)
				.ToListAsync(cancellationToken);

			if (alerts.Count == 0)
			{
				await transaction.CommitAsync(cancellationToken);
				return 0;
			}

			// Every alert in this batch only needs opportunities published after its
			// own LastNotifiedAt, which is always >= this batch-wide minimum - so a
			// single query bounded by the minimum still covers every alert's
			// candidates; the per-alert cutoff is re-applied below.
			var earliestCursor = alerts.Min(s => s.LastNotifiedAt);

			var candidates = await dbContext.Set<VolunteerOpportunity>()
				.AsNoTracking()
				.Where(vo =>
					vo.Status == OpportunityStatus.Published &&
					vo.PublishedOn != null &&
					vo.PublishedOn > earliestCursor)
				.ToListAsync(cancellationToken);

			var claimedMessages = new List<OutboxMessage>(alerts.Count);
			var occurredOnUtc = now.UtcDateTime;

			foreach (var alert in alerts)
			{
				var previousLastNotifiedAt = alert.LastNotifiedAt;

				var matchedIds = candidates
					.Where(vo => vo.PublishedOn > previousLastNotifiedAt && alert.Matches(vo))
					.Select(vo => vo.Id.Value)
					.ToList();

				// Atomic per-row claim: this UPDATE's WHERE clause is re-evaluated by
				// Postgres against the row's current committed state, so if another
				// replica's tick already claimed this alert, it affects 0 rows
				// instead of racing to a duplicate digest - unlike a plain
				// tracked-entity SaveChangesAsync, which would have no guard against
				// exactly that race.
				var rowsAffected = await dbContext.Set<SearchAlert>()
					.Where(s => s.Id == alert.Id && s.LastNotifiedAt == previousLastNotifiedAt)
					.ExecuteUpdateAsync(
						u => u
							.SetProperty(s => s.LastNotifiedAt, now)
							.SetProperty(s => s.ModifiedOn, now),
						cancellationToken);

				if (rowsAffected == 0 || matchedIds.Count == 0)
					continue;

				var domainEvent = new SearchAlertMatchesFoundDomainEvent(alert.Id, alert.UserId, matchedIds);
				claimedMessages.Add(OutboxMessage.FromDomainEvent(domainEvent, occurredOnUtc));
			}

			if (claimedMessages.Count == 0)
			{
				await transaction.CommitAsync(cancellationToken);
				return 0;
			}

			// ExecuteUpdateAsync above bypasses the ChangeTracker, so
			// ConvertDomainEventsToOutboxMessagesInterceptor never sees these events -
			// the outbox rows are built directly here instead, then written in a
			// single batched SaveChangesAsync for however many this tick actually won.
			dbContext.Set<OutboxMessage>().AddRange(claimedMessages);
			await dbContext.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);

			return claimedMessages.Count;
		}, cancellationToken);
	}
}
