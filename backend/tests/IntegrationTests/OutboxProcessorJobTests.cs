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

using CoreDomainEvent = Domain.Primitives.DomainEvent;

namespace IntegrationTests;

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
	public async Task ProcessBatchAsync_DispatchFails_SchedulesExponentialBackoffAndReleasesTheClaim(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var beforeAttempt = DateTime.UtcNow;
		await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, new RecordingDispatcher { ThrowOnDispatch = true }, NullLogger.Instance, new OutboxMetrics(meterFactory),
			batchSize: 20, retryBackoffBaseSeconds: 60, cancellationToken: cancellationToken);

		var message = await dbContext.Set<OutboxMessage>()
			.AsNoTracking()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ClaimedOnUtc.Should().BeNull("the claim must be released so a different replica can pick this message up once the backoff elapses");
		message.NextAttemptOnUtc.Should().NotBeNull();
		message.NextAttemptOnUtc!.Value.Should().BeCloseTo(beforeAttempt.AddSeconds(120), TimeSpan.FromSeconds(10),
			"attempt 1 backs off 2^1 * retryBackoffBaseSeconds, not an immediate retry");
	}

	[Test]
	public async Task ProcessBatchAsync_MessageWithFutureNextAttempt_IsNotReclaimedBeforeItElapses(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.AttemptCount = 1;
		message.NextAttemptOnUtc = DateTime.UtcNow.AddMinutes(10);
		await dbContext.SaveChangesAsync(cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var claimed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		claimed.Should().Be(0, "a message backing off must not be reclaimed before its NextAttemptOnUtc elapses, even though ClaimedOnUtc is null");
		dispatcher.DispatchedEvents.Should().BeEmpty();
	}

	[Test]
	public async Task ProcessBatchAsync_MessageWithElapsedNextAttempt_IsReclaimedAndDispatched(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		await SeedOutboxMessageAsync(dbContext, domainEvent, cancellationToken);

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.AttemptCount = 1;
		message.NextAttemptOnUtc = DateTime.UtcNow.AddSeconds(-1);
		await dbContext.SaveChangesAsync(cancellationToken);

		using var meterFactory = new TestMeterFactory();
		var dispatcher = new RecordingDispatcher();
		var claimed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		claimed.Should().Be(1, "the backoff has elapsed, so the message must be eligible for another attempt");
		dispatcher.DispatchedEvents.Should().ContainSingle();
	}

	[Test]
	public async Task ProcessBatchAsync_PoisonMessage_StillDispatchesTheOtherMessagesInTheBatch(
		CancellationToken cancellationToken)
	{
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

		var realAttempts = 0;
		for (var round = 0; round < maxAttempts * 5 && realAttempts < maxAttempts; round++)
		{
			// A real failure now backs off NextAttemptOnUtc into the future - force
			// it back into the past between rounds so this test can drive every attempt
			// without waiting out the real backoff delay.
			await dbContext.Set<OutboxMessage>()
				.Where(m => m.Type == PoisonMessageType)
				.ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptOnUtc, DateTime.UtcNow.AddSeconds(-1)), cancellationToken);

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

		var processedOnNextPoll = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, new RecordingDispatcher(), NullLogger.Instance, metrics,
			batchSize: 20, maxAttempts: maxAttempts, cancellationToken: cancellationToken);

		processedOnNextPoll.Should().Be(0, "a dead-lettered message must not stall every message behind it forever");
	}

	[Test]
	public async Task ProcessBatchAsync_RoundTripsGuidBackedValueObjectIds_NotAsGuidEmpty(
		CancellationToken cancellationToken)
	{
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
	public async Task ProcessBatchAsync_MultipleMessages_PersistsEachMessageBeforeDispatchingTheNext(
		CancellationToken cancellationToken)
	{
		await using var seedContext = fixture.CreateApplicationDbContext();
		var firstEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		var secondEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		var baseTime = DateTime.UtcNow;
		var firstMessage = OutboxMessage.FromDomainEvent(firstEvent, baseTime);
		var secondMessage = OutboxMessage.FromDomainEvent(secondEvent, baseTime.AddMilliseconds(1));
		seedContext.Set<OutboxMessage>().AddRange(firstMessage, secondMessage);
		await seedContext.SaveChangesAsync(cancellationToken);

		var secondDispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseSecondDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var dispatcher = new SequencedDispatcher(secondDispatchStarted, releaseSecondDispatch.Task);

		await using var processingContext = fixture.CreateApplicationDbContext();
		using var meterFactory = new TestMeterFactory();

		var processTask = OutboxProcessorJob.ProcessBatchAsync(
			processingContext, dispatcher, NullLogger.Instance, new OutboxMetrics(meterFactory), batchSize: 20, cancellationToken: cancellationToken);

		await secondDispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

		// The second message's dispatch is still gated (simulating the process being
		// killed before ProcessBatchAsync returns) - a separate connection must already
		// see the first message's processed state durably committed, proving it was not
		// deferred to a single SaveChangesAsync at the end of the whole batch.
		await using var verifyContext = fixture.CreateApplicationDbContext();
		var firstMessageState = await verifyContext.Set<OutboxMessage>()
			.AsNoTracking()
			.SingleAsync(m => m.Id == firstMessage.Id, cancellationToken);
		firstMessageState.ProcessedOnUtc.Should().NotBeNull(
			"the first message's processed state must be durably persisted before the batch moves on to dispatching the next message");

		releaseSecondDispatch.SetResult();
		var processed = await processTask;

		processed.Should().Be(2);
	}

	[Test]
	public async Task ProcessBatchAsync_TwoConcurrentCalls_SecondCallDoesNotReclaimAMessageBeingDispatchedByTheFirst(
		CancellationToken cancellationToken)
	{
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

	private sealed class SequencedDispatcher(TaskCompletionSource secondCallStarted, Task releaseSecondCall) : IDomainEventDispatcher
	{
		private int _callCount;

		public async ValueTask DispatchAsync(IEnumerable<CoreDomainEvent> events, CancellationToken cancellationToken = default)
		{
			if (Interlocked.Increment(ref _callCount) == 2)
			{
				secondCallStarted.TrySetResult();
				await releaseSecondCall;
			}
		}
	}

	private sealed class GatedDispatcher(TaskCompletionSource started, Task releaseGate) : IDomainEventDispatcher
	{
		public async ValueTask DispatchAsync(IEnumerable<CoreDomainEvent> events, CancellationToken cancellationToken = default)
		{
			started.TrySetResult();
			await releaseGate;
		}
	}
}
