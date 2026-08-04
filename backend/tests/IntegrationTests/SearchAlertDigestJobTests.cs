using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.SearchAlerts;
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

// Exercises Infrastructure.BackgroundJobs.SearchAlertDigestJob.ClaimAndQueueMatchesAsync
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real integration
// Postgres - the job's own PeriodicTimer only fires daily, and the interesting behavior
// here (#1090) is the atomic per-alert claim that prevents two replicas' ticks from both
// queuing a digest for the same alert, which is only provable by calling it twice
// concurrently against two independent ApplicationDbContexts/connections.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class SearchAlertDigestJobTests(IntegrationTestFixture fixture)
{
	private const string MatchesFoundDomainEventType = "Domain.SearchAlerts.SearchAlertMatchesFoundDomainEvent";

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ClaimAndQueueMatchesAsync_OpportunityPublishedAfterLastNotifiedAt_ClaimsItAndQueuesOutboxMessage(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (alertId, _) = await SeedAlertAndOpportunityAsync(
			dbContext, lastNotifiedAt: now.AddDays(-1), publishedOn: now.AddHours(-1), categories: [], cancellationToken);

		var queued = await SearchAlertDigestJob.ClaimAndQueueMatchesAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(1);

		var lastNotifiedAt = await dbContext.Set<SearchAlert>()
			.AsNoTracking()
			.Where(s => s.Id == alertId)
			.Select(s => s.LastNotifiedAt)
			.SingleAsync(cancellationToken);
		lastNotifiedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1), "claiming must advance the cursor so the same match isn't queued again");

		(await fixture.CountOutboxMessagesOfTypeAsync(MatchesFoundDomainEventType)).Should().Be(1);
	}

	[Test]
	public async Task ClaimAndQueueMatchesAsync_OpportunityPublishedBeforeLastNotifiedAt_DoesNotMatch(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		await SeedAlertAndOpportunityAsync(
			dbContext, lastNotifiedAt: now.AddHours(-1), publishedOn: now.AddDays(-1), categories: [], cancellationToken);

		var queued = await SearchAlertDigestJob.ClaimAndQueueMatchesAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(0);
		(await fixture.CountOutboxMessagesOfTypeAsync(MatchesFoundDomainEventType)).Should().Be(0);
	}

	[Test]
	public async Task ClaimAndQueueMatchesAsync_CriteriaDoesNotMatch_AdvancesCursorButQueuesNothing(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		// The seeded opportunity has no category (see SeedAlertAndOpportunityAsync) - a
		// category-scoped alert can never match it.
		var (alertId, _) = await SeedAlertAndOpportunityAsync(
			dbContext, lastNotifiedAt: now.AddDays(-1), publishedOn: now.AddHours(-1), categories: ["Environment"], cancellationToken);

		var queued = await SearchAlertDigestJob.ClaimAndQueueMatchesAsync(dbContext, now, 500, cancellationToken);

		queued.Should().Be(0);
		(await fixture.CountOutboxMessagesOfTypeAsync(MatchesFoundDomainEventType)).Should().Be(0);

		var lastNotifiedAt = await dbContext.Set<SearchAlert>()
			.AsNoTracking()
			.Where(s => s.Id == alertId)
			.Select(s => s.LastNotifiedAt)
			.SingleAsync(cancellationToken);
		lastNotifiedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1), "the cursor must still advance on a non-matching tick, or every future tick would keep rescanning the same old opportunity forever");
	}

	[Test]
	public async Task ClaimAndQueueMatchesAsync_TwoConcurrentCallsAgainstTheSameAlert_OnlyOneClaimsIt(
		CancellationToken cancellationToken)
	{
		// Simulates two replicas' SearchAlertDigestJob ticks racing over the same due
		// alert - the same class of bug EngagementReminderJob's #1392 guarded against.
		// Two independent ApplicationDbContexts (separate connections) so the atomic
		// per-row claim is genuinely exercised at the database level, not just
		// serialized by sharing one DbContext/connection.
		await using var seedContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (alertId, _) = await SeedAlertAndOpportunityAsync(
			seedContext, lastNotifiedAt: now.AddDays(-1), publishedOn: now.AddHours(-1), categories: [], cancellationToken);

		await using var contextA = fixture.CreateApplicationDbContext();
		await using var contextB = fixture.CreateApplicationDbContext();

		var results = await Task.WhenAll(
			SearchAlertDigestJob.ClaimAndQueueMatchesAsync(contextA, now, 500, cancellationToken),
			SearchAlertDigestJob.ClaimAndQueueMatchesAsync(contextB, now, 500, cancellationToken));

		results.Sum().Should().Be(1, "exactly one of the two concurrent ticks should have won the claim");

		var lastNotifiedAt = await seedContext.Set<SearchAlert>()
			.AsNoTracking()
			.Where(s => s.Id == alertId)
			.Select(s => s.LastNotifiedAt)
			.SingleAsync(cancellationToken);
		lastNotifiedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));

		(await fixture.CountOutboxMessagesOfTypeAsync(MatchesFoundDomainEventType)).Should().Be(
			1, "the losing replica must not have queued a second digest for the same alert");
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static async Task<(SearchAlertId AlertId, VolunteerOpportunityId OpportunityId)> SeedAlertAndOpportunityAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset lastNotifiedAt,
		DateTimeOffset publishedOn,
		IReadOnlyCollection<string> categories,
		CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"SearchAlertTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var opportunity = VolunteerOpportunity.Create(
			organization.Id, "Beach Cleanup", "Help clean the beach", true, null,
			Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, new NoOpPinGenerator(),
			status: OpportunityStatus.Published, validUntil: DateTimeOffset.UtcNow.AddDays(14), now: publishedOn).Value;
		dbContext.Set<VolunteerOpportunity>().Add(opportunity);

		var alert = SearchAlert.Create(UserId.New(), null, null, null, null, null, null, categories, now: lastNotifiedAt);
		dbContext.Set<SearchAlert>().Add(alert);

		await dbContext.SaveChangesAsync(cancellationToken);

		return (alert.Id, opportunity.Id);
	}

	private sealed class NoOpPinGenerator : IPinGenerator
	{
		public string GeneratePin() => "0000";
	}
}
