using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;
// ApiClient.cs (generated, same "IntegrationTests" namespace) also declares
// "Organization"/"OrganizationId" DTO types, which would otherwise shadow the domain
// types of the same name pulled in via the "Domain.Organizations" using above.
using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

// Exercises Infrastructure.BackgroundJobs.EngagementReminderJob.ClaimAndQueueRemindersAsync
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real integration
// Postgres - the job's own PeriodicTimer only fires hourly, and the interesting behavior
// here (#1392) is the atomic per-row claim that prevents two replicas' ticks from both
// queuing a reminder for the same engagement, which is only provable by calling it twice
// concurrently against two independent ApplicationDbContexts/connections.
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
		// Simulates two replicas' EngagementReminderJob ticks racing over the same due
		// engagement - the bug #1392 describes for the real job (duplicate 24h reminder
		// emails). Two independent ApplicationDbContexts (separate connections) so the
		// atomic per-row claim is genuinely exercised at the database level, not just
		// serialized by sharing one DbContext/connection.
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

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static async Task<(EngagementId EngagementId, VolunteerOpportunityId OpportunityId, TimeSlotId TimeSlotId)> SeedConfirmedEngagementAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset timeSlotStart,
		DateTimeOffset? reminderSentAt,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"ReminderTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		// Waitlist opportunities can't be created directly as Published (they must have
		// at least one time slot first - see VolunteerOpportunity.Create) - Draft is fine
		// here since nothing in this test path depends on the opportunity being published.
		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Beach Cleanup", "Help clean the beach", true, null,
			Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).Value;

		var timeSlot = opportunity.AddTimeSlot(
			timeSlotStart, timeSlotStart.AddHours(2), 10, DateTimeOffset.UtcNow).Value;
		dbContext.Set<VolunteerOpportunity>().Add(opportunity);

		var engagement = Engagement.CreateWaitlistSignUp(opportunity.Id, UserId.New(), timeSlot.Id);
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
}
