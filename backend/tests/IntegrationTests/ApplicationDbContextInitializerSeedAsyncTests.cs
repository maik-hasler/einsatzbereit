using Application.Common.Exceptions;
using Application.Common.Keycloak;
using AwesomeAssertions;
using Infrastructure.Persistence;
using Infrastructure.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
