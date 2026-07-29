using Application.Common;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
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

		var dispatcher = new RecordingDispatcher();
		var processed = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, batchSize: 20, cancellationToken);

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

		var dispatcher = new RecordingDispatcher { ThrowOnDispatch = true };
		var attempted = await OutboxProcessorJob.ProcessBatchAsync(
			dbContext, dispatcher, NullLogger.Instance, batchSize: 20, cancellationToken);

		attempted.Should().Be(1, "the message was selected and attempted even though dispatch failed");

		var message = await dbContext.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ProcessedOnUtc.Should().BeNull("a failed dispatch must leave the message unprocessed so the next poll cycle retries it");
		message.Error.Should().NotBeNullOrEmpty();
	}

	[Test]
	public async Task ProcessBatchAsync_TwoConcurrentCalls_OnlyOneDispatchesTheLockedMessage(
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

		var taskA = OutboxProcessorJob.ProcessBatchAsync(
			contextA, gatedDispatcher, NullLogger.Instance, batchSize: 20, cancellationToken);

		// Wait until A's transaction has actually reached the dispatcher - meaning its
		// SELECT ... FOR UPDATE SKIP LOCKED already committed the row lock - before
		// starting B, so B's own SKIP LOCKED query has something real to skip instead of
		// racing to see an as-yet-unlocked row.
		await started.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

		var recordingDispatcher = new RecordingDispatcher();
		var selectedByB = await OutboxProcessorJob.ProcessBatchAsync(
			contextB, recordingDispatcher, NullLogger.Instance, batchSize: 20, cancellationToken);

		selectedByB.Should().Be(0, "the only pending message is locked by A's still-open transaction, so B's SKIP LOCKED query must skip it rather than dispatch it a second time");
		recordingDispatcher.DispatchedEvents.Should().BeEmpty();

		release.SetResult();
		var processedByA = await taskA;

		processedByA.Should().Be(1);

		var message = await seedContext.Set<OutboxMessage>()
			.AsNoTracking()
			.SingleAsync(m => m.Type == typeof(EngagementConfirmedDomainEvent).FullName, cancellationToken);
		message.ProcessedOnUtc.Should().NotBeNull();
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static async Task SeedOutboxMessageAsync(
		ApplicationDbContext dbContext, CoreDomainEvent domainEvent, CancellationToken cancellationToken)
	{
		dbContext.Set<OutboxMessage>().Add(OutboxMessage.FromDomainEvent(domainEvent, DateTime.UtcNow));
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
