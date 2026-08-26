using System.Text.RegularExpressions;
using Application.Achievements.BadgeCatalog;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using AwesomeAssertions;
using Domain.Achievements;
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

using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class ApplicationDbContextInitializerSeedAsyncTests(IntegrationTestFixture fixture)
{
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

		var existingOrgId = keycloak.SeedExistingOrganization("Lindenauer Nachbarschaftshilfe e.V.");

		var initializer = new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), new FakeBadgeCatalogService(), NullLogger<ApplicationDbContextInitializer>.Instance);

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

		keycloak.SeedExistingOrganization("Lindenauer Nachbarschaftshilfe e.V.", OlafId, VeraId);
		keycloak.SeedExistingOrganization("Lindenauer Tierschutzverein e.V.", OlafId);
		keycloak.SeedExistingOrganizerRole(OlafId);

		var initializer = new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), new FakeBadgeCatalogService(), NullLogger<ApplicationDbContextInitializer>.Instance);

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
			dbContext, keycloak, new RandomPinGenerator(), new FakeBadgeCatalogService(), NullLogger<ApplicationDbContextInitializer>.Instance);

		Func<Task> act = async () => await initializer.SeedAsync(cancellationToken);

		await act.Should().ThrowAsync<InvalidOperationException>(
			"a Keycloak failure must surface instead of being silently logged and swallowed (#1212)");

		(await dbContext.Set<DomainOrganization>().AnyAsync(cancellationToken)).Should().BeFalse(
			"nothing should have been persisted - SaveChangesAsync never had a chance to run");
	}

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
			new FakeBadgeCatalogService(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var organizations = await dbContext.Set<DomainOrganization>().ToListAsync(cancellationToken);
		var opportunities = await dbContext.Set<VolunteerOpportunity>().ToListAsync(cancellationToken);

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

	[Test]
	public async Task SeedAsync_SeedsDaytimeSlotsThatDoNotRunOvernight(CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext,
			new FakeKeycloakOrganizationService(),
			new RandomPinGenerator(),
			new FakeBadgeCatalogService(),
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

	[Test]
	public async Task SeedAsync_SeedsAPastCheckedInEngagementAwaitingFeedback_ForTheRatingFlowToBeTestable(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext, new FakeKeycloakOrganizationService(), new RandomPinGenerator(), new FakeBadgeCatalogService(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

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

	[Test]
	public async Task SeedAsync_AwardsFirstStepAchievementForVeraSeededPastEngagement_SoTheProfileIsNotSelfContradictory(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext, new FakeKeycloakOrganizationService(), new RandomPinGenerator(), new FakeBadgeCatalogService(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

		await initializer.SeedAsync(cancellationToken);

		var veraUserId = UserId.Create(VeraId).GetValueOrThrow();

		var streak = await dbContext.GetUserStreakAsync(veraUserId, cancellationToken);
		streak.Should().NotBeNull(
			"Vera's seeded past engagement is Confirmed, so a real confirmation through the normal flow would have "
			+ "created her streak row too");
		streak!.TotalConfirmedEngagements.Should().BeGreaterThanOrEqualTo(1,
			"her one seeded confirmed engagement must count toward the milestone gate, or the profile's header "
			+ "count and her badge progress silently disagree with what actually earns badges (#2229)");

		var achievements = await dbContext.Set<Achievement>()
			.Where(a => a.UserId == veraUserId)
			.ToListAsync(cancellationToken);
		achievements.Should().Contain(a => a.Key == "first-step",
			"seeding Vera's past engagement as already Confirmed bypasses ConfirmEngagementCommandHandler, the only "
			+ "place that normally awards \"first-step\" - without seeding the award to match, her profile shows "
			+ "100% badge progress for a badge she was never actually granted (#2229)");
	}

	[Test]
	public async Task SeedAsync_SeedsLocalMembershipRowForVera_SoHerOwnOrganizationsQueryFindsIt(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		var initializer = new ApplicationDbContextInitializer(
			dbContext, new FakeKeycloakOrganizationService(), new RandomPinGenerator(), new FakeBadgeCatalogService(),
			NullLogger<ApplicationDbContextInitializer>.Instance);

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

	[Test]
	public async Task SeedAsync_DatabaseAlreadyHasOrganizations_SkipsAndWarnsTheSeedSetWasNotApplied(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();

		await new ApplicationDbContextInitializer(
			dbContext,
			new FakeKeycloakOrganizationService(),
			new RandomPinGenerator(),
			new FakeBadgeCatalogService(),
			NullLogger<ApplicationDbContextInitializer>.Instance).SeedAsync(cancellationToken);

		var logger = new FakeLogger<ApplicationDbContextInitializer>();
		var keycloak = new FakeKeycloakOrganizationService();

		await new ApplicationDbContextInitializer(
			dbContext, keycloak, new RandomPinGenerator(), new FakeBadgeCatalogService(), logger).SeedAsync(cancellationToken);

		keycloak.CreateOrganizationCallCount.Should().Be(0, "seeding must not run a second time");
		(await dbContext.Set<DomainOrganization>().CountAsync(cancellationToken)).Should().Be(
			2, "the existing data is left exactly as it was");

		var record = logger.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Warning).Subject;
		record.Message.Should().Contain("NOT",
			"the warning has to say the seed set was not applied, not just that seeding was skipped");
		record.Message.Should().Contain("Wipe the database",
			"an operator reading this line needs to be told what to do about it");
	}

	private sealed class FakeBadgeCatalogService : IBadgeCatalogService
	{
		public IReadOnlyList<Application.Achievements.BadgeCatalog.BadgeCatalogEntry> GetAll() => [];

		public Application.Achievements.BadgeCatalog.BadgeCatalogEntry? FindByKey(string key) =>
			new(key, AchievementType.Milestone, key, key, IsHidden: false);
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

		public Task<KeycloakOrganizationMember?> FindUserByExactMatchAsync(
			string search, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();
	}
}
