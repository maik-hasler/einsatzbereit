using System.Diagnostics.Metrics;
using Application.Common;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core.Interfaces;
// ApiClient.cs (generated, same "IntegrationTests" namespace) also declares a
// "DomainEvent" DTO type, which would otherwise shadow Domain.Primitives.DomainEvent.
using CoreDomainEvent = Domain.Primitives.DomainEvent;

namespace IntegrationTests;

// Exercises Infrastructure.BackgroundJobs.OutboxProcessorJob.ProcessBatchAsync directly
// (InternalsVisibleTo, see Infrastructure.csproj) against the real integration Postgres.
// The interesting behavior here (#1392) is the FOR UPDATE SKIP LOCKED row-claiming that
// stops two replicas' timers from both dispatching the same pending message - only
// provable by holding one call's transaction open (via a gated fake dispatcher) while a
// second concurrent call proves it skips the locked row instead of double-dispatching.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OutboxProcessorJobTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ProcessBatchAsync_PendingMessage_DispatchesItAndMarksProcessed(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var processed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		processed.Should().Be(1);
		dispatcher.DispatchedEvents.Should().ContainSingle();

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ProcessedOnUtc.Should().NotBeNull();
		message.Error.Should().BeNull();
	}

	[Test]
	public async Task ProcessBatchAsync_DispatchThrows_RecordsErrorAndLeavesMessageUnprocessedForRetry(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher { ThrowOnDispatch = true };
		var attempted = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		attempted.Should().Be(1, "the message was selected and attempted even though dispatch failed");

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ProcessedOnUtc.Should().BeNull("a failed dispatch must leave the message unprocessed so the next poll cycle retries it");
		message.Error.Should().NotBeNullOrEmpty();
		message.AttemptCount.Should().Be(1);
	}

	[Test]
	public async Task ProcessBatchAsync_PoisonMessage_StillDispatchesTheOtherMessagesInTheBatch(
		CancellationToken cancellationToken)
	{
		// Regression for #1317: before the attempt cap, a message whose Type can't be
		// resolved (a renamed/removed domain event) would throw on every single poll
		// forever - but critically it was never the *only* thing that mattered here,
		// since ORDER BY occurred_on_utc means a poison row at the head of the batch
		// must not prevent healthy rows behind it from being dispatched.
		await using var dbContext = fixture.CreateApplicationDbContext();
		await SeedPoisonOutboxMessageAsync(dbContext, cancellationToken);
		var healthyEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, healthyEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var attempted = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		attempted.Should().Be(2);
		dispatcher.DispatchedEvents.Should().ContainSingle().Subject.Should().BeOfType<EngagementConfirmedDomainEvent>();

		var poisonMessage = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == PoisonMessageType, cancellationToken);
		poisonMessage.ProcessedOnUtc.Should().BeNull("one failed attempt must not yet exceed the default max attempts");
		poisonMessage.AttemptCount.Should().Be(1);
		poisonMessage.Error.Should().NotBeNullOrEmpty();

		var healthyMessage = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		healthyMessage.ProcessedOnUtc.Should().NotBeNull();
		healthyMessage.Error.Should().BeNull();
	}

	[Test]
	public async Task ProcessBatchAsync_MessageExceedingMaxAttempts_MovesToDeadLetterStateAndStopsBeingRetried(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		await SeedPoisonOutboxMessageAsync(dbContext, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var metrics = new OutboxMetrics(meterFactory);

		const int maxAttempts = 3;

		// The fixture boots the real app, whose own OutboxProcessorJob hosted service
		// is concurrently polling this same table on its 5s timer (unfiltered - it
		// claims any unprocessed row, including this one). Its FOR UPDATE SKIP LOCKED
		// query can occasionally win the race against one of the calls below, which
		// then silently claims zero rows that round instead of incrementing. Retry a
		// round that claimed nothing rather than assuming exactly `maxAttempts` calls
		// always means `maxAttempts` real attempts.
		var realAttempts = 0;
		for (var round = 0; round < maxAttempts * 5 && realAttempts < maxAttempts; round++)
		{
			var claimed = await OutboxProcessorJob.ProcessBatchAsync(
				dbContext, new RecordingDispatcher(), NullLogger.Instance, metrics,
				batchSize: 20, maxAttempts: maxAttempts, cancellationToken: cancellationToken);
			if (claimed > 0)
				realAttempts++;
		}
		realAttempts.Should().Be(maxAttempts, "the retry budget above should comfortably absorb the live job's occasional interference");

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == PoisonMessageType, cancellationToken);
		message.AttemptCount.Should().Be(maxAttempts);
		message.ProcessedOnUtc.Should().NotBeNull(
			"a message that has exhausted its retry budget must move to a terminal dead-letter state");
		message.Error.Should().NotBeNullOrEmpty(
			"the populated Error distinguishes a dead-lettered message from a genuinely successful dispatch");

		// One more poll cycle must not re-select the now-terminal poison row.
		var processedOnNextPoll = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, new RecordingDispatcher(), NullLogger.Instance, metrics,
			batchSize: 20, maxAttempts: maxAttempts, cancellationToken: cancellationToken);

		processedOnNextPoll.Should().Be(0, "a dead-lettered message must not stall every message behind it forever");
	}

	[Test]
	public async Task ProcessBatchAsync_RoundTripsGuidBackedValueObjectIds_NotAsGuidEmpty(
		CancellationToken cancellationToken)
	{
		// Regression test: these Guid-backed value-object IDs have a private constructor and
		// a get-only Value property, so System.Text.Json's default reflection-based
		// deserializer has no way to populate them - it was silently producing a
		// Guid.Empty-backed instance instead of throwing (see
		// ValueObjectIdJsonConverterFactory), which einsatzbereit#1038's cascade-cancel tests
		// caught: the first outbox-dispatched handler to actually look an entity up by the
		// deserialized id rather than just logging it.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		var redispatched = dispatcher.DispatchedEvents.Should().ContainSingle().Subject
			.Should().BeOfType<EngagementConfirmedDomainEvent>().Subject;

		redispatched.EngagementId.Should().Be(domainEvent.EngagementId);
		redispatched.VolunteerId.Should().Be(domainEvent.VolunteerId);
		redispatched.OpportunityId.Should().Be(domainEvent.OpportunityId);
	}

	[Test]
	public async Task ProcessBatchAsync_TwoConcurrentCalls_SecondCallDoesNotReclaimAMessageBeingDispatchedByTheFirst(
		CancellationToken cancellationToken)
	{
		// Regression for #1729: dispatch now happens with no open transaction/row
		// lock held (see OutboxProcessorJob.ClaimBatchAsync) so it no longer blocks
		// on a synchronous SMTP send per organizer for the whole batch. What now
		// stops a second replica from re-selecting the same message while A is
		// still dispatching it is ClaimedOnUtc, stamped and committed by A's short
		// claim transaction before dispatch even starts.
		await using var seedContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(seedContext, domainEvent, cancellationToken);

		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var gatedDispatcher = new GatedDispatcher(started, release.Task);

		await using var contextA = fixture.CreateApplicationDbContext();
		await using var contextB = fixture.CreateApplicationDbContext();
		using var meterFactory = new TestMeterFactory();
		var metrics = new OutboxMetrics(meterFactory);

		var taskA = OutboxProcessorJob.ProcessBatchAsync(
			contextA, gatedDispatcher, NullLogger.Instance, metrics, batchSize: 20, cancellationToken: cancellationToken);

		// Wait until A has claimed the message (ClaimedOnUtc committed) and reached
		// the dispatcher - A's claim transaction has already committed by this
		// point, so nothing is locked anymore; only the ClaimedOnUtc stamp
		// protects the row now.
		await started.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

		var recordingDispatcher = new RecordingDispatcher();
		var claimedByB = await OutboxProcessorJob.ProcessBatchAsync(
			contextB, recordingDispatcher, NullLogger.Instance, metrics, batchSize: 20, cancellationToken: cancellationToken);

		claimedByB.Should().Be(0, "the message was just claimed by A - within the claim timeout, B's claim query must skip it rather than dispatch it a second time");
		recordingDispatcher.DispatchedEvents.Should().BeEmpty();

		release.SetResult();
		var processedByA = await taskA;

		processedByA.Should().Be(1);

		var message = await seedContext.Set<OutboxMessage>()
			.AsNoTracking()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ProcessedOnUtc.Should().NotBeNull();
	}

	[Test]
	public async Task ProcessBatchAsync_MessageClaimedPastTheTimeout_IsReclaimedAndDispatched(
		CancellationToken cancellationToken)
	{
		// Regression for #1729: a process that claims a batch and then crashes
		// before dispatch completes must not leave those messages stuck forever -
		// once ClaimedOnUtc is older than claimTimeoutSeconds, a later poll treats
		// the claim as abandoned and reclaims the message.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ClaimedOnUtc = DateTime.UtcNow.AddSeconds(-10);
		await dbContext.SaveChangesAsync(cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var claimed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory),
			batchSize: 20, claimTimeoutSeconds: 5, cancellationToken: cancellationToken);

		claimed.Should().Be(1, "a claim older than claimTimeoutSeconds must be treated as abandoned and reclaimed");
		dispatcher.DispatchedEvents.Should().ContainSingle();

		var reprocessed = await dbContext.Set<OutboxMessage>()
			.AsNoTracking()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		reprocessed.ProcessedOnUtc.Should().NotBeNull();
	}

	[Test]
	public async Task ProcessBatchAsync_MessageClaimedWithinTheTimeout_IsNotReclaimed(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ClaimedOnUtc = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var claimed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory),
			batchSize: 20, claimTimeoutSeconds: 300, cancellationToken: cancellationToken);

		claimed.Should().Be(0, "a message claimed moments ago is presumed still in flight elsewhere");
		dispatcher.DispatchedEvents.Should().BeEmpty();
	}

	// #1008: OutboxMessage.Error was persisted but never surfaced anywhere an operator
	// could see it - these prove the outbox.dispatch/outbox.pending metrics (recorded in
	// OutboxProcessorJob.ProcessBatchAsync) give that visibility instead.
	[Test]
	public async Task ProcessBatchAsync_DispatchSucceeds_RecordsDispatchedMetricAndClearsPendingBacklog(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var metrics = new OutboxMetrics(meterFactory);
		var recorded = RecordOutboxMeasurements(meterFactory);

		var dispatcher = new RecordingDispatcher();
		await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, metrics, batchSize: 20, cancellationToken: cancellationToken);

		recorded.Should().ContainSingle(m => m.Instrument == "outbox.dispatch" && m.Status == "succeeded" && m.Value == 1);
		recorded.Should().ContainSingle(m => m.Instrument == "outbox.pending" && m.Value == 0,
			"the only pending message was dispatched successfully, leaving no backlog");
	}

	[Test]
	public async Task ProcessBatchAsync_DispatchFails_RecordsFailedMetricAndKeepsMessageInPendingBacklog(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var metrics = new OutboxMetrics(meterFactory);
		var recorded = RecordOutboxMeasurements(meterFactory);

		var dispatcher = new RecordingDispatcher { ThrowOnDispatch = true };
		await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, metrics, batchSize: 20, cancellationToken: cancellationToken);

		recorded.Should().ContainSingle(m => m.Instrument == "outbox.dispatch" && m.Status == "failed" && m.Value == 1);
		recorded.Should().ContainSingle(m => m.Instrument == "outbox.pending" && m.Value == 1,
			"the failed message stays unprocessed, still counting toward the backlog");
	}

	[Test]
	public async Task ProcessBatchAsync_NoPendingMessages_RecordsZeroPendingBacklogAndNoDispatchMeasurements(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		using var meterFactory = new TestMeterFactory();
		var metrics = new OutboxMetrics(meterFactory);
		var recorded = RecordOutboxMeasurements(meterFactory);

		var dispatcher = new RecordingDispatcher();
		var processed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, metrics, batchSize: 20, cancellationToken: cancellationToken);

		processed.Should().Be(0);
		recorded.Should().NotContain(m => m.Instrument == "outbox.dispatch");
		recorded.Should().ContainSingle(m => m.Instrument == "outbox.pending" && m.Value == 0);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private const string PoisonMessageType = "Domain.NoLongerExists.RenamedOrRemovedDomainEvent";

	private static List<(string Instrument, string? Status, long Value)> RecordOutboxMeasurements(IMeterFactory meterFactory)
	{
		var recorded = new List<(string, string?, long)>();

		var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Meter.Name == OutboxMetrics.MeterName)
					l.EnableMeasurementEvents(instrument);
			},
		};
		listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			var status = tags.ToArray().FirstOrDefault(t => t.Key == "status").Value?.ToString();
			recorded.Add((instrument.Name, status, measurement));
		});
		listener.Start();

		return recorded;
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
				meter.Dispose();
		}
	}

	private static async Task SeedOutboxMessageAsync(
		ApplicationDbContext dbContext, CoreDomainEvent domainEvent, CancellationToken cancellationToken)
	{
		dbContext.Set<OutboxMessage>().Add(OutboxMessage.FromDomainEvent(domainEvent, DateTime.UtcNow));
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	// Simulates the poison-message scenario from #1317 - a Type that OutboxMessage.ToDomainEvent()
	// can never resolve (e.g. a domain event class that was since renamed or removed) - without
	// needing an actual removed type to reference.
	private static async Task SeedPoisonOutboxMessageAsync(
		ApplicationDbContext dbContext, CancellationToken cancellationToken)
	{
		dbContext.Set<OutboxMessage>().Add(new OutboxMessage
		{
			Id = Guid.NewGuid(),
			Type = PoisonMessageType,
			Content = "{}",
			OccurredOnUtc = DateTime.UtcNow,
		});
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private sealed class RecordingDispatcher : IDomainEventDispatcher
	{
		public List<CoreDomainEvent> DispatchedEvents { get; } = [];

		public bool ThrowOnDispatch { get; set; }

		public ValueTask DispatchAsync(IEnumerable<CoreDomainEvent> events, CancellationToken cancellationToken = default)
		{
			if (ThrowOnDispatch)
				throw new InvalidOperationException("Simulated dispatch failure.");

			DispatchedEvents.AddRange(events);
			return ValueTask.CompletedTask;
		}
	}

	// Signals `started` the instant DispatchAsync is entered (i.e. after ProcessBatchAsync's
	// SELECT ... FOR UPDATE SKIP LOCKED has already run and committed its row lock), then
	// blocks until the test releases it - simulating a slow dispatch so a concurrent
	// second call's SKIP LOCKED query has a genuinely still-locked row to skip.
	private sealed class GatedDispatcher(TaskCompletionSource started, Task releaseGate) : IDomainEventDispatcher
	{
		public async ValueTask DispatchAsync(IEnumerable<CoreDomainEvent> events, CancellationToken cancellationToken = default)
		{
			started.TrySetResult();
			await releaseGate;
		}
	}
}
