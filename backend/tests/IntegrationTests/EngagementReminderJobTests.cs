using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TUnit.Core.Interfaces;

using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EngagementReminderJobTests(IntegrationTestFixture fixture)
{
	private const string ReminderDueDomainEventType = "Domain.Engagements.EngagementReminderDueDomainEvent";

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ClaimAndQueueRemindersAsync_EngagementDueForReminder_ClaimsItAndQueuesOutboxMessage(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotStart: now.AddHours(24), reminderSentAt: null, cancellationToken);

		var queued = await EngagementReminderJob.ClaimAndQueueRemindersAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(1);

		var reminderSentAt = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.ReminderSentAt)
			.SingleAsync(cancellationToken);
		reminderSentAt.Should().NotBeNull("claiming must stamp ReminderSentAt so the same engagement isn't picked up again");

		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(1);
	}

	[Test]
	public async Task ClaimAndQueueRemindersAsync_TimeSlotOutsideReminderWindow_DoesNotClaim(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotStart: now.AddDays(5), reminderSentAt: null, cancellationToken);

		var queued = await EngagementReminderJob.ClaimAndQueueRemindersAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(0);

		var reminderSentAt = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.ReminderSentAt)
			.SingleAsync(cancellationToken);
		reminderSentAt.Should().BeNull();

		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndQueueRemindersAsync_EngagementConfirmedLessThan23HoursBeforeSlot_StillQueuesReminder(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotStart: now.AddHours(2), reminderSentAt: null, cancellationToken);

		var queued = await EngagementReminderJob.ClaimAndQueueRemindersAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(1, "a shift starting in under 23 hours still deserves a reminder, just later than the ideal 24h mark");

		var reminderSentAt = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.ReminderSentAt)
			.SingleAsync(cancellationToken);
		reminderSentAt.Should().NotBeNull();

		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(1);
	}

	[Test]
	public async Task ClaimAndQueueRemindersAsync_SlotAlreadyStarted_MarksReminderSentWithoutQueuing(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, timeSlotId) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotStart: now.AddHours(2), reminderSentAt: null, cancellationToken);

		// The domain forbids creating a time slot that starts in the past, so create it
		// in the future and move it back afterward to simulate time having passed it.
		await dbContext.Set<TimeSlot>()
			.Where(ts => ts.Id == timeSlotId)
			.ExecuteUpdateAsync(s => s.SetProperty(ts => ts.StartDateTime, now.AddHours(-1)), cancellationToken);

		var queued = await EngagementReminderJob.ClaimAndQueueRemindersAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(0, "a shift that already started must not receive a reminder for something already underway");

		var reminderSentAt = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.ReminderSentAt)
			.SingleAsync(cancellationToken);
		reminderSentAt.Should().NotBeNull(
			"it must still be marked so a long-passed shift doesn't linger forever as a pending reminder");

		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndQueueRemindersAsync_AlreadyReminded_DoesNotClaimAgain(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		await SeedConfirmedEngagementAsync(
			dbContext, timeSlotStart: now.AddHours(24), reminderSentAt: now.AddMinutes(-5), cancellationToken);

		var queued = await EngagementReminderJob.ClaimAndQueueRemindersAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(0);
		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndQueueRemindersAsync_TwoConcurrentCallsAgainstTheSameEngagement_OnlyOneClaimsIt(
		CancellationToken cancellationToken)
	{
		await using var seedContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			seedContext, timeSlotStart: now.AddHours(24), reminderSentAt: null, cancellationToken);

		await using var contextA = fixture.CreateApplicationDbContext();
		await using var contextB = fixture.CreateApplicationDbContext();

		var results = await Task.WhenAll(
			EngagementReminderJob.ClaimAndQueueRemindersAsync(contextA, now, 500, cancellationToken),
			EngagementReminderJob.ClaimAndQueueRemindersAsync(contextB, now, 500, cancellationToken));

		results.Sum().Should().Be(1, "exactly one of the two concurrent ticks should have won the claim");

		var reminderSentAt = await seedContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.ReminderSentAt)
			.SingleAsync(cancellationToken);
		reminderSentAt.Should().NotBeNull();

		(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(
			1, "the losing replica must not have queued a second reminder for the same engagement");
	}

	[Test]
	public async Task StartAsync_EngagementDueForReminder_QueuesItWithoutWaitingForFirstHourlyTick(
		CancellationToken cancellationToken)
	{
		await using var seedContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			seedContext, timeSlotStart: now.AddHours(24), reminderSentAt: null, cancellationToken);

		using var scopeFactory = new SingleContextScopeFactory(fixture.CreateApplicationDbContext);
		var job = new EngagementReminderJob(
			scopeFactory,
			NullLogger<EngagementReminderJob>.Instance,
			Options.Create(new EngagementReminderOptions { MaxBatchSize = 500, PollIntervalHours = 24 }));

		await job.StartAsync(cancellationToken);
		try
		{
			var deadline = DateTime.UtcNow.AddSeconds(10);
			int remindedCount;
			do
			{
				await Task.Delay(200, cancellationToken);
				remindedCount = await seedContext.Set<Engagement>()
					.AsNoTracking()
					.Where(e => e.Id == engagementId && e.ReminderSentAt != null)
					.CountAsync(cancellationToken);
			}
			while (remindedCount == 0 && DateTime.UtcNow < deadline);

			remindedCount.Should().Be(
				1, "starting the job should tick immediately instead of waiting a full PollIntervalHours");
			(await fixture.CountOutboxMessagesOfTypeAsync(ReminderDueDomainEventType)).Should().Be(1);
		}
		finally
		{
			await job.StopAsync(cancellationToken);
		}
	}

	private static async Task<(EngagementId EngagementId, VolunteerOpportunityId OpportunityId, TimeSlotId TimeSlotId)> SeedConfirmedEngagementAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset timeSlotStart,
		DateTimeOffset? reminderSentAt,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"ReminderTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Beach Cleanup", null, "Help clean the beach", null, true, null,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).Value;

		var timeSlot = opportunity.AddTimeSlot(
			timeSlotStart, timeSlotStart.AddHours(2), 10, DateTimeOffset.UtcNow).Value;
		dbContext.Set<VolunteerOpportunity>().Add(opportunity);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), timeSlot.Id);
		engagement.Confirm();
		dbContext.Set<Engagement>().Add(engagement);

		await dbContext.SaveChangesAsync(cancellationToken);

		if (reminderSentAt.HasValue)
		{
			await dbContext.Set<Engagement>()
				.Where(e => e.Id == engagement.Id)
				.ExecuteUpdateAsync(s => s.SetProperty(e => e.ReminderSentAt, reminderSentAt), cancellationToken);
		}

		return (engagement.Id, opportunity.Id, timeSlot.Id);
	}

	private sealed class NoOpPinGenerator : IPinGenerator
	{
		public string GeneratePin() => "0000";
	}

	private sealed class SingleContextScopeFactory(Func<ApplicationDbContext> contextFactory)
		: IServiceScopeFactory, IDisposable
	{
		private readonly List<ApplicationDbContext> _createdContexts = [];

		public IServiceScope CreateScope()
		{
			var dbContext = contextFactory();
			_createdContexts.Add(dbContext);
			return new Scope(dbContext);
		}

		public void Dispose()
		{
			foreach (var dbContext in _createdContexts)
				dbContext.Dispose();
		}

		private sealed class Scope(ApplicationDbContext dbContext) : IServiceScope
		{
			public IServiceProvider ServiceProvider { get; } = new Provider(dbContext);

			public void Dispose()
			{
			}
		}

		private sealed class Provider(ApplicationDbContext dbContext) : IServiceProvider
		{
			public object? GetService(Type serviceType) =>
				serviceType == typeof(ApplicationDbContext) ? dbContext : null;
		}
	}
}
