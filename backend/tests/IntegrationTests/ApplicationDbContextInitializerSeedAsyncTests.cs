using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Infrastructure.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core.Interfaces;
// ApiClient.cs (generated, same "IntegrationTests" namespace) also declares an
// "Organization"/"OrganizationId" DTO, which would otherwise shadow the domain types.
using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

// Exercises ApplicationDbContextInitializer.SeedAsync directly (InternalsVisibleTo, see
// Infrastructure.csproj) against the real integration Postgres, standing a hand-written
// fake in for the real KeycloakOrganizationService. The real "backend" Aspire resource
// already ran its own one-shot SeedAsync at boot and had it wiped by IntegrationTestFixture's
// own reset before any test runs, so this is the only way to reproduce "Keycloak already
// has this organization/membership/role from a prior, partially-failed seed attempt" and
// prove the fix for #1212: organization creation is idempotent by name, and a Keycloak
// failure now propagates instead of being silently swallowed.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class ApplicationDbContextInitializerSeedAsyncTests(IntegrationTestFixture fixture)
{
	// Duplicated from ApplicationDbContextInitializer's own private constants - same
	// reasoning as AspireFixture.OlafId (VisualTests): a fixed literal, not worth
	// exposing cross-assembly just for this.
	private static readonly Guid OlafId = new("00000000-0000-0000-0000-000000000001");
	private static readonly Guid VeraId = new("00000000-0000-0000-0000-000000000002");

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task SeedAsync_OrganizationAlreadyExistsInKeycloakFromPriorPartialFailure_ReusesItInsteadOfCreatingDuplicate(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var keycloak = new FakeKeycloakOrganizationService();

		// Simulates the exact failure mode from #1212: a prior seed attempt got far
		// enough to create the first organization in Keycloak, then failed before
		// the trailing SaveChangesAsync - so the local database is still empty, but
		// re-seeding must not create a second, orphaned copy of that org.
		var existingOrgId = keycloak.SeedExistingOrganization("Lindenauer Nachbarschaftshilfe e.V.");

		var initializer = new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		keycloak.FindOrganizationByNameCallCount.Should().Be(2, "both seed organizations are looked up by name before creating one");
		keycloak.CreateOrganizationCallCount.Should().Be(
			1, "the first organization already existed in Keycloak and must not be created a second time");

		var organizations = await dbContext.Set<DomainOrganization>().ToListAsync(cancellationToken);
		organizations.Should().HaveCount(2);
		organizations.Should().Contain(o => o.Id == DomainOrganizationId.Create(existingOrgId).GetValueOrThrow());
	}

	[Test]
	public async Task SeedAsync_MembersAndOrganizerRoleAlreadyPresentFromPriorPartialFailure_DoesNotReapplyThem(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var keycloak = new FakeKeycloakOrganizationService();

		// Simulates a prior attempt that got further still: both organizations, all
		// their members, and olaf's organizer role were already provisioned in
		// Keycloak before the trailing SaveChangesAsync failed.
		keycloak.SeedExistingOrganization("Lindenauer Nachbarschaftshilfe e.V.", OlafId, VeraId);
		keycloak.SeedExistingOrganization("Lindenauer Tierschutzverein e.V.", OlafId);
		keycloak.SeedExistingOrganizerRole(OlafId);

		var initializer = new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		keycloak.CreateOrganizationCallCount.Should().Be(0, "both organizations already existed in Keycloak");
		keycloak.AddMemberCallCount.Should().Be(0, "every member had already been added in the prior attempt");
		keycloak.AssignOrganizerRoleCallCount.Should().Be(0, "olaf already held the organizer role from the prior attempt");

		var organizations = await dbContext.Set<DomainOrganization>().ToListAsync(cancellationToken);
		organizations.Should().HaveCount(2, "seeding must still complete and persist locally even though nothing needed to change in Keycloak");
	}

	[Test]
	public async Task SeedAsync_KeycloakCallFails_PropagatesExceptionInsteadOfSwallowingIt(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var keycloak = new FakeKeycloakOrganizationService
		{
			ThrowOnCreateOrganizationFor = _ => true,
		};

		var initializer = new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), NullLogger<ApplicationDbContextInitializer>.Instance);

		Func<Task> act = async () => await initializer.SeedAsync(cancellationToken);

		await act.Should().ThrowAsync<InvalidOperationException>(
			"a Keycloak failure must surface instead of being silently logged and swallowed (#1212)");

		(await dbContext.Set<DomainOrganization>().AnyAsync(cancellationToken)).Should().BeFalse(
			"nothing should have been persisted - SaveChangesAsync never had a chance to run");
	}

	// #1776: staging served "First Aid Course" / "Fairview Animal Welfare Association" /
	// "+1 555 0100" under German chrome for months. The seed set itself had already been
	// translated by then - what nothing checked was that it *stays* translated, so this
	// pins the property the issue actually asserts: the default locale's demo content
	// matches the default locale (German is what the app serves by default, see root
	// AGENTS.md).
	//
	// Matching on English function words rather than on the exact German strings is
	// deliberate. An expected-titles list would be a change-detector - every legitimate
	// edit to the seed set would fail it, and it would be updated without thought. These
	// words are only the ones with no German homograph, so they cannot appear in correct
	// German prose but appear almost immediately in English prose; "in", "an", "was",
	// "will" and "so" are all excluded precisely because German uses them too.
	private static readonly string[] EnglishMarkers =
	[
		"the", "and", "with", "for", "your", "our", "you", "we", "from", "this", "that",
		"help", "join", "learn", "course", "volunteer", "association", "welfare", "aid",
	];

	[Test]
	public async Task SeedAsync_SeedsDemoContentInTheDefaultServedLocale(CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext,
			new FakeKeycloakOrganizationService(),
			new RandomPinGenerator(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var organizations = await dbContext.Set<DomainOrganization>().ToListAsync(cancellationToken);
		var opportunities = await dbContext.Set<VolunteerOpportunity>().ToListAsync(cancellationToken);

		// Prose only - emails and websites are deliberately not scanned, since a host
		// like "nachbarschaftshilfe-lindenau.example" is not prose and has no locale.
		var prose = organizations.Select(o => o.Name)
			.Concat(organizations.Select(o => o.Description ?? string.Empty))
			.Concat(opportunities.Select(o => o.TitleDe))
			.Concat(opportunities.Select(o => o.DescriptionDe))
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.ToList();

		prose.Should().NotBeEmpty("the seed set has organizations and opportunities to check");

		foreach (var text in prose)
		{
			var hits = EnglishMarkers
				.Where(marker => Regex.IsMatch(text, $@"\b{marker}\b", RegexOptions.IgnoreCase))
				.ToList();

			hits.Should().BeEmpty(
				"seeded demo content is what a visitor to the German UI reads, so it must be German too - "
				+ $"\"{text}\" reads as English ({string.Join(", ", hits)})");
		}

		organizations.Should().OnlyContain(
			o => o.ContactPhone != null && o.ContactPhone.StartsWith("+49"),
			"a German contact block showing a +1 555 number is the same half-translated seam as English copy");
	}

	// The other half of the same finding: staging advertised the one-day
	// "Erste-Hilfe-Kurs" as running 23:05-07:05 across two dates, because slot times
	// used to inherit DateTimeOffset.UtcNow's time-of-day - whatever o'clock the seeder
	// happened to boot at. ApplicationDbContextInitializer.DayAt pins them instead;
	// this is what keeps it pinned, since the bug only ever showed up on a deployment
	// that restarted at an unlucky hour and never in a local run.
	[Test]
	public async Task SeedAsync_SeedsDaytimeSlotsThatDoNotRunOvernight(CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext,
			new FakeKeycloakOrganizationService(),
			new RandomPinGenerator(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var slots = (await dbContext.Set<VolunteerOpportunity>()
				.Include(o => o.TimeSlots)
				.ToListAsync(cancellationToken))
			.SelectMany(o => o.TimeSlots.Select(slot => (o.TitleDe, slot.StartDateTime, slot.EndDateTime)))
			.ToList();

		slots.Should().NotBeEmpty("the seed set publishes slot-based opportunities");

		foreach (var (title, start, end) in slots)
		{
			var startUtc = start.UtcDateTime;
			var endUtc = end.UtcDateTime;

			startUtc.Hour.Should().BeInRange(6, 20,
				$"\"{title}\" is demo content a visitor reads as a real shift, and {startUtc:HH:mm} UTC is not a "
				+ "time anyone volunteers at");
			endUtc.Date.Should().Be(startUtc.Date,
				$"\"{title}\" advertises a shift within one day, so it must not cross midnight "
				+ $"({startUtc:yyyy-MM-dd HH:mm} - {endUtc:yyyy-MM-dd HH:mm} UTC)");
		}
	}

	// #1909: staging's "Erste-Hilfe-Kurs" sign-up was found already showing "checked in"
	// and "feedback given" while its time slot was still 13 days out - internally
	// inconsistent, and leaving no way to reach a genuine "past, completed, awaiting
	// feedback" example to test the rating flow against (Engagement.CheckIn() has no
	// time-based guard, so that combination was most likely staging test debris rather
	// than the seed set itself, but the seed set never offered a clean example either).
	// The seed set now provides that example directly instead of relying on it existing
	// by accident.
	[Test]
	public async Task SeedAsync_SeedsAPastCheckedInEngagementAwaitingFeedback_ForTheRatingFlowToBeTestable(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext, new FakeKeycloakOrganizationService(), new RandomPinGenerator(), NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var veraUserId = UserId.Create(VeraId).GetValueOrThrow();
		var opportunity = await dbContext.Set<VolunteerOpportunity>()
			.Include(o => o.TimeSlots)
			.SingleAsync(o => o.TitleDe == "Erste-Hilfe-Kurs", cancellationToken);

		var engagements = await dbContext.Set<Engagement>()
			.Where(e => e.VolunteerId == veraUserId && e.OpportunityId == opportunity.Id)
			.ToListAsync(cancellationToken);
		var pastEngagement = engagements.Should().ContainSingle(e => e.IsCheckedIn).Subject;
		var pastTimeSlot = opportunity.TimeSlots.Single(ts => ts.Id == pastEngagement.TimeSlotId);

		pastTimeSlot.EndDateTime.Should().BeBefore(DateTimeOffset.UtcNow,
			"a future-dated slot paired with a checked-in/feedback-given engagement is exactly the "
			+ "inconsistency #1909 was filed against");
		pastEngagement.Status.Should().Be(EngagementStatus.Confirmed);
		pastEngagement.FeedbackSubmittedAt.Should().BeNull(
			"feedback must still be outstanding, or there is nothing left to test the rating flow against (#1909)");

		opportunity.TimeSlots.Should().Contain(ts => ts.EndDateTime >= DateTimeOffset.UtcNow,
			"the opportunity must keep a non-elapsed slot too, or it silently drops out of the public "
			+ "listing entirely (ApplyPubliclyListedFilters) once its only slot is in the past");
	}

	// #1846: Vera is a genuine, Keycloak-confirmed member of org1 - EnsureMemberAsync
	// added her there - but SeedOrg1Async only ever wrote a local OrganizationMembership
	// row for Olaf (the organizer), never for Vera. GetMemberOrganizationsAsync (the
	// query behind GET /v1/organizations, "my organizations") reads only that local
	// table, so Vera's own account saw zero organizations despite the org's own Members
	// page (Keycloak-sourced) correctly listing her as an active member.
	[Test]
	public async Task SeedAsync_SeedsLocalMembershipRowForVera_SoHerOwnOrganizationsQueryFindsIt(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext, new FakeKeycloakOrganizationService(), new RandomPinGenerator(), NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var veraUserId = UserId.Create(VeraId).GetValueOrThrow();
		var veraOrganizations = await dbContext.GetMemberOrganizationsAsync(veraUserId, cancellationToken);

		veraOrganizations.Should().ContainSingle(o => o.Name == "Lindenauer Nachbarschaftshilfe e.V.",
			"Vera is a real, Keycloak-confirmed member of this organization - her own \"my organizations\" query "
			+ "must see the same membership the organization's own Members page shows for her (#1846)");

		var veraMembership = await dbContext.Set<OrganizationMembership>()
			.SingleAsync(m => m.UserId == veraUserId, cancellationToken);
		veraMembership.Role.Should().Be(OrganizationMemberRole.Member, "Vera was seeded as a plain member, not an organizer");
	}

	// The guard itself is correct and stays - re-seeding a populated database would have
	// to delete rows that are no longer demo data. What #1776 cost was the silence: a
	// long-lived environment restarts, skips seeding, and looks exactly like one that
	// seeded successfully. This pins the log line that tells them apart.
	[Test]
	public async Task SeedAsync_DatabaseAlreadyHasOrganizations_SkipsAndWarnsTheSeedSetWasNotApplied(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		await new ApplicationDbContextInitializer(
			dbContext,
			new FakeKeycloakOrganizationService(),
			new RandomPinGenerator(),
			NullLogger<ApplicationDbContextInitializer>.Instance).SeedAsync(cancellationToken);

		var logger = new FakeLogger<ApplicationDbContextInitializer>();
		var keycloak = new FakeKeycloakOrganizationService();

		// A second boot of an environment that is already seeded - what every staging
		// restart since the seed set was translated has actually been doing.
		await new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), logger).SeedAsync(cancellationToken);

		keycloak.CreateOrganizationCallCount.Should().Be(0, "seeding must not run a second time");
		(await dbContext.Set<DomainOrganization>().CountAsync(cancellationToken)).Should().Be(
			2, "the existing data is left exactly as it was");

		var record = logger.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Warning).Subject;
		record.Message.Should().Contain("NOT",
			"the warning has to say the seed set was not applied, not just that seeding was skipped");
		record.Message.Should().Contain("reset-staging.yml",
			"an operator reading this line needs to be told what to do about it");
	}

	private sealed class FakeKeycloakOrganizationService : IKeycloakOrganizationService
	{
		private readonly Dictionary<string, Guid> _organizationsByName = [];
		private readonly Dictionary<Guid, HashSet<Guid>> _membersByOrganization = [];
		private readonly HashSet<Guid> _organisatorUserIds = [];

		public int CreateOrganizationCallCount { get; private set; }

		public int FindOrganizationByNameCallCount { get; private set; }

		public int AddMemberCallCount { get; private set; }

		public int AssignOrganizerRoleCallCount { get; private set; }

		public Func<string, bool>? ThrowOnCreateOrganizationFor { get; set; }

		public Guid SeedExistingOrganization(string name, params Guid[] existingMemberIds)
		{
			var id = Guid.NewGuid();
			_organizationsByName[name] = id;
			_membersByOrganization[id] = [.. existingMemberIds];
			return id;
		}

		public void SeedExistingOrganizerRole(Guid userId) => _organisatorUserIds.Add(userId);

		public Task<Guid> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default)
		{
			CreateOrganizationCallCount++;

			if (ThrowOnCreateOrganizationFor?.Invoke(name) == true)
				throw new InvalidOperationException($"Simulated Keycloak failure creating '{name}'.");

			var id = Guid.NewGuid();
			_organizationsByName[name] = id;
			_membersByOrganization[id] = [];
			return Task.FromResult(id);
		}

		public Task<Guid?> FindOrganizationByNameAsync(string name, CancellationToken cancellationToken = default)
		{
			FindOrganizationByNameCallCount++;
			return Task.FromResult(_organizationsByName.TryGetValue(name, out var id) ? id : (Guid?)null);
		}

		public Task AddMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
		{
			AddMemberCallCount++;
			_membersByOrganization[organizationId].Add(userId);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<KeycloakOrganizationMember>> GetMembersAsync(
			Guid organizationId, CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<KeycloakOrganizationMember>>(
				_membersByOrganization[organizationId]
					.Select(userId => new KeycloakOrganizationMember(
						userId, "user", null, null, "user@example.com", _organisatorUserIds.Contains(userId)))
					.ToList());

		public Task AssignOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken = default)
		{
			AssignOrganizerRoleCallCount++;
			_organisatorUserIds.Add(userId);
			return Task.CompletedTask;
		}

		public Task<IReadOnlySet<Guid>> GetRealmOrganisatorUserIdsAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlySet<Guid>>(_organisatorUserIds.ToHashSet());

		public Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task DeleteOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task RevokeOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<IReadOnlyList<KeycloakOrganizationMember>> SearchUsersAsync(
			string search, int max = 20, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();
	}
}
