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

// Exercises Infrastructure.BackgroundJobs.AutomaticCheckInJob.ClaimAndCheckInAsync
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real integration
// Postgres - the job's own PeriodicTimer only fires hourly, and the interesting behavior
// here (einsatzbereit#1042) is the atomic per-row claim that prevents two replicas'
// ticks from both checking in (and raising a duplicate domain event for) the same
// engagement, which is only provable by calling it twice concurrently against two
// independent ApplicationDbContexts/connections.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AutomaticCheckInJobTests(IntegrationTestFixture fixture)
{
	private const string CheckedInDomainEventType = "Domain.Engagements.EngagementCheckedInDomainEvent";

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ClaimAndCheckInAsync_EndedSlotWithCheckInMethodNone_ChecksInAndQueuesOutboxMessage(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotEnd: now.AddHours(-1), CheckInMethod.None, isCheckedIn: false, cancellationToken);

		var checkedIn = await AutomaticCheckInJob.ClaimAndCheckInAsync(dbContext, now, 500, cancellationToken);

		checkedIn.Should().Be(1);

		var isCheckedIn = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.IsCheckedIn)
			.SingleAsync(cancellationToken);
		isCheckedIn.Should().BeTrue("an ended slot on a CheckInMethod.None opportunity must be auto-checked-in");

		(await fixture.CountOutboxMessagesOfTypeAsync(CheckedInDomainEventType)).Should().Be(1);
	}

	[Test]
	public async Task ClaimAndCheckInAsync_TimeSlotNotYetEnded_DoesNotClaim(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotEnd: now.AddHours(1), CheckInMethod.None, isCheckedIn: false, cancellationToken);

		var checkedIn = await AutomaticCheckInJob.ClaimAndCheckInAsync(dbContext, now, 500, cancellationToken);

		checkedIn.Should().Be(0);

		var isCheckedIn = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.IsCheckedIn)
			.SingleAsync(cancellationToken);
		isCheckedIn.Should().BeFalse();

		(await fixture.CountOutboxMessagesOfTypeAsync(CheckedInDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndCheckInAsync_CheckInMethodNotNone_DoesNotClaim(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			dbContext, timeSlotEnd: now.AddHours(-1), CheckInMethod.Manual, isCheckedIn: false, cancellationToken);

		var checkedIn = await AutomaticCheckInJob.ClaimAndCheckInAsync(dbContext, now, 500, cancellationToken);

		checkedIn.Should().Be(0, "an opportunity with an explicit check-in method must not be auto-checked-in");

		var isCheckedIn = await dbContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.IsCheckedIn)
			.SingleAsync(cancellationToken);
		isCheckedIn.Should().BeFalse();

		(await fixture.CountOutboxMessagesOfTypeAsync(CheckedInDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndCheckInAsync_AlreadyCheckedIn_DoesNotClaimAgain(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		await SeedConfirmedEngagementAsync(
			dbContext, timeSlotEnd: now.AddHours(-1), CheckInMethod.None, isCheckedIn: true, cancellationToken);

		var checkedIn = await AutomaticCheckInJob.ClaimAndCheckInAsync(dbContext, now, 500, cancellationToken);

		checkedIn.Should().Be(0);
		(await fixture.CountOutboxMessagesOfTypeAsync(CheckedInDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndCheckInAsync_TwoConcurrentCallsAgainstTheSameEngagement_OnlyOneClaimsIt(
		CancellationToken cancellationToken)
	{
		// Simulates two replicas' AutomaticCheckInJob ticks racing over the same ended
		// slot - two independent ApplicationDbContexts (separate connections) so the
		// atomic per-row claim is genuinely exercised at the database level, not just
		// serialized by sharing one DbContext/connection.
		await using var seedContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (engagementId, _, _) = await SeedConfirmedEngagementAsync(
			seedContext, timeSlotEnd: now.AddHours(-1), CheckInMethod.None, isCheckedIn: false, cancellationToken);

		await using var contextA = fixture.CreateApplicationDbContext();
		await using var contextB = fixture.CreateApplicationDbContext();

		var results = await Task.WhenAll(
			AutomaticCheckInJob.ClaimAndCheckInAsync(contextA, now, 500, cancellationToken),
			AutomaticCheckInJob.ClaimAndCheckInAsync(contextB, now, 500, cancellationToken));

		results.Sum().Should().Be(1, "exactly one of the two concurrent ticks should have won the claim");

		var isCheckedIn = await seedContext.Set<Engagement>()
			.AsNoTracking()
			.Where(e => e.Id == engagementId)
			.Select(e => e.IsCheckedIn)
			.SingleAsync(cancellationToken);
		isCheckedIn.Should().BeTrue();

		(await fixture.CountOutboxMessagesOfTypeAsync(CheckedInDomainEventType)).Should().Be(
			1, "the losing replica must not have queued a second check-in event for the same engagement");
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static async Task<(EngagementId EngagementId, VolunteerOpportunityId OpportunityId, TimeSlotId TimeSlotId)> SeedConfirmedEngagementAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset timeSlotEnd,
		CheckInMethod checkInMethod,
		bool isCheckedIn,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"AutoCheckInTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		// ScheduledSlots opportunities can't be created directly as Published (they must have
		// at least one time slot first - see VolunteerOpportunity.Create) - Draft is fine
		// here since nothing in this test path depends on the opportunity being published.
		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Beach Cleanup", "Help clean the beach", true, null,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, checkInMethod, new NoOpPinGenerator(),
			status: OpportunityStatus.Draft).Value;

		var timeSlotStart = timeSlotEnd.AddHours(-2);
		// TimeSlot.Create rejects a start date <= "now" (see TimeSlot.Validate), so an
		// already-ended slot can't be created against the real clock - pass an
		// artificially-past "now" here instead, well before timeSlotStart, so domain
		// validation passes regardless of whether timeSlotStart/timeSlotEnd are
		// themselves in the past relative to the real clock.
		var creationNow = timeSlotStart.AddDays(-1);
		var timeSlot = opportunity.AddTimeSlot(timeSlotStart, timeSlotEnd, 10, creationNow).Value;
		dbContext.Set<VolunteerOpportunity>().Add(opportunity);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), timeSlot.Id);
		engagement.Confirm();
		dbContext.Set<Engagement>().Add(engagement);

		await dbContext.SaveChangesAsync(cancellationToken);

		if (isCheckedIn)
		{
			await dbContext.Set<Engagement>()
				.Where(e => e.Id == engagement.Id)
				.ExecuteUpdateAsync(s => s.SetProperty(e => e.IsCheckedIn, true), cancellationToken);
		}

		return (engagement.Id, opportunity.Id, timeSlot.Id);
	}

	private sealed class NoOpPinGenerator : IPinGenerator
	{
		public string GeneratePin() => "0000";
	}
}
