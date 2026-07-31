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

// Opportunities using CheckInMethod.None are labelled as "automatic" check-in, but
// until this job existed nothing ever set IsCheckedIn for them - attendance and
// feedback (which requires IsCheckedIn) were permanently dead for that
// configuration (einsatzbereit#1042). This job finds confirmed, not-yet-checked-in
// engagements whose time slot has ended on a CheckInMethod.None opportunity and
// marks them checked in, following the same atomic claim-per-row pattern as
// EngagementReminderJob so concurrent replicas can never double-process the same
// engagement.
internal sealed class AutomaticCheckInJob(
	IServiceScopeFactory scopeFactory,
	ILogger<AutomaticCheckInJob> logger,
	IOptions<AutomaticCheckInOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly AutomaticCheckInOptions _options = options.Value;

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
				// Mirrors EngagementReminderJob/OutboxProcessorJob: a failure here (e.g. a
				// transient DB error) must not stop the PeriodicTimer from ever being
				// awaited again. Any due engagement still has IsCheckedIn == false, so it
				// is simply picked up again on the next tick.
				logger.LogError(ex, "Automatic check-in tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var checkedIn = await ClaimAndCheckInAsync(dbContext, DateTimeOffset.UtcNow, _options.MaxBatchSize, ct);

		if (checkedIn > 0)
			logger.LogInformation("Automatically checked in {Count} engagement(s)", checkedIn);
	}

	// Exposed so IntegrationTests can exercise the claim race directly against a real
	// ApplicationDbContext - e.g. two concurrent calls racing over the same ended
	// time slot, without waiting an hour for a real tick from two replicas.
	internal static async Task<int> ClaimAndCheckInAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset now,
		int maxBatchSize,
		CancellationToken cancellationToken = default)
	{
		// EnableRetryOnFailure (ServiceCollectionExtensions.cs) requires a
		// manually-began transaction to run as one retryable unit via
		// CreateExecutionStrategy() - see
		// ApplicationDbContext.ExecuteInTransactionAsync's comment and
		// EngagementReminderJob's matching wrapper for why retrying this
		// whole delegate from scratch is safe.
		var strategy = dbContext.Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync<int>(async _ =>
		{
			// One transaction for the whole claim-and-enqueue batch - see
			// EngagementReminderJob.ClaimAndQueueRemindersAsync for why: without it, a
			// crash between the ExecuteUpdateAsync claims and the outbox SaveChangesAsync
			// below would leave engagements marked IsCheckedIn with no outbox row ever
			// written for the domain event, losing it silently instead of retrying.
			await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

			// Only ScheduledSlots engagements carry a TimeSlotId, so IndividualContact
			// opportunities (which have no time slot, hence no well-defined "event end")
			// are not candidates here - the same limitation EngagementReminderJob already
			// has for its 24h-before-start window.
			var candidates = await dbContext.Set<Engagement>()
				.Where(e =>
					e.Status == EngagementStatus.Confirmed &&
					!e.IsCheckedIn &&
					e.TimeSlotId != null)
				.Join(
					dbContext.Set<TimeSlot>(),
					e => e.TimeSlotId,
					ts => ts.Id,
					(e, ts) => new { e.Id, e.VolunteerId, e.OpportunityId, ts.EndDateTime })
				.Join(
					dbContext.Set<VolunteerOpportunity>(),
					x => x.OpportunityId,
					vo => vo.Id,
					(x, vo) => new { x.Id, x.VolunteerId, x.OpportunityId, x.EndDateTime, vo.CheckInMethod })
				.Where(x => x.EndDateTime <= now && x.CheckInMethod == CheckInMethod.None)
				.OrderBy(x => x.EndDateTime)
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
				// Atomic per-row claim, same reasoning as EngagementReminderJob: if another
				// replica's tick already claimed this engagement, this affects 0 rows
				// instead of racing to check it in (and raise its domain event) twice.
				var rowsAffected = await dbContext.Set<Engagement>()
					.Where(e => e.Id == candidate.Id && !e.IsCheckedIn)
					.ExecuteUpdateAsync(
						s => s
							.SetProperty(e => e.IsCheckedIn, true)
							.SetProperty(e => e.ModifiedOn, now),
						cancellationToken);

				if (rowsAffected == 0)
					continue;

				var domainEvent = new EngagementCheckedInDomainEvent(
					candidate.Id, candidate.VolunteerId!.Value, candidate.OpportunityId);
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
		}, cancellationToken);
	}
}
