using Application.Common.Exceptions;
using Application.Common.Keycloak;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.StartupTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core.Interfaces;

using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizationMembershipBackfillJobTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task BackfillAsync_PreExistingOrganizationWithoutMembershipRows_InsertsRowPerDistinctMember(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var organizationId = await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);

		var organizerId = Guid.NewGuid();
		var memberId = Guid.NewGuid();
		var keycloak = new FakeKeycloakOrganizationService
		{
			MembersToReturn =
			[
				new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "O.", "olaf@example.com", IsOrganisator: true),

				new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "O.", "olaf@example.com", IsOrganisator: true),

				new KeycloakOrganizationMember(memberId, "vera", "Vera", "V.", "vera@example.com", IsOrganisator: false),
			],
		};

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		var memberships = await dbContext.Set<OrganizationMembership>()
			.Where(m => m.OrganizationId == organizationId)
			.ToListAsync(cancellationToken);

		memberships.Should().HaveCount(2);

		var organizerMembership = memberships.Should().ContainSingle(m => m.UserId == UserId.Create(organizerId).GetValueOrThrow())
			.Subject;
		organizerMembership.Role.Should().Be(OrganizationMemberRole.Organizer);

		var memberMembership = memberships.Should().ContainSingle(m => m.UserId == UserId.Create(memberId).GetValueOrThrow())
			.Subject;
		memberMembership.Role.Should().Be(OrganizationMemberRole.Member);

		var marker = await dbContext.Set<OrganizationMembershipBackfillState>().SingleOrDefaultAsync(cancellationToken);
		marker.Should().NotBeNull("a completed run must leave the one-shot marker behind");
	}

	[Test]
	public async Task BackfillAsync_MultiplePreExistingOrganizations_FetchesRealmOrganizerSetOnlyOnce(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);
		await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);

		var organizerId = Guid.NewGuid();
		var keycloak = new FakeKeycloakOrganizationService
		{
			MembersToReturn =
			[
				new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "O.", "olaf@example.com", IsOrganisator: true),
			],
		};

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		keycloak.GetMembersCallCount.Should().Be(2, "each of the two pre-existing organizations still needs its own member list");
		keycloak.GetRealmOrganisatorCallCount.Should().Be(
			1, "the realm-wide organizer set is shared across every organization in the same run, not re-fetched per organization");
	}

	[Test]
	public async Task BackfillAsync_OrganizationHasNoOrganizers_StillWritesCompletionMarkerAndNeverRetries(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var organizationId = await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);

		var keycloak = new FakeKeycloakOrganizationService { MembersToReturn = [] };

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		(await dbContext.Set<OrganizationMembership>().AnyAsync(m => m.OrganizationId == organizationId, cancellationToken))
			.Should().BeFalse();
		(await dbContext.Set<OrganizationMembershipBackfillState>().AnyAsync(cancellationToken))
			.Should().BeTrue("the one-shot marker must be written even when no organization actually needed rows added");

		keycloak.GetMembersCallCount.Should().Be(1);

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		keycloak.GetMembersCallCount.Should().Be(
			1, "a second run must short-circuit on the marker instead of re-querying Keycloak for the still-organizer-less organization");
	}

	[Test]
	public async Task BackfillAsync_MarkerAlreadyPresent_NeverCallsKeycloak(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);

		dbContext.Set<OrganizationMembershipBackfillState>().Add(
			new OrganizationMembershipBackfillState { CompletedOnUtc = DateTime.UtcNow });
		await dbContext.SaveChangesAsync(cancellationToken);

		var keycloak = new FakeKeycloakOrganizationService
		{
			MembersToReturn = [new KeycloakOrganizationMember(Guid.NewGuid(), "olaf", null, null, "olaf@example.com", IsOrganisator: true)],
		};

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		keycloak.GetMembersCallCount.Should().Be(0, "an already-completed run must skip straight past every organization check");
	}

	[Test]
	public async Task BackfillAsync_KeycloakFails_DoesNotWriteMarkerSoALaterRunCanStillRetry(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var organizationId = await SeedOrganizationWithoutMembershipRowsAsync(dbContext, cancellationToken);

		var keycloak = new FakeKeycloakOrganizationService { ThrowOnGetMembers = true };

		var act = () => OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		await act.Should().NotThrowAsync("a transient Keycloak failure must be swallowed, not crash the caller");
		(await dbContext.Set<OrganizationMembershipBackfillState>().AnyAsync(cancellationToken))
			.Should().BeFalse("a failed run must not be marked complete, so the next boot retries instead of skipping real work");

		keycloak.ThrowOnGetMembers = false;
		keycloak.MembersToReturn = [new KeycloakOrganizationMember(Guid.NewGuid(), "olaf", null, null, "olaf@example.com", IsOrganisator: true)];

		await OrganizationMembershipBackfillJob.BackfillAsync(
			dbContext, keycloak, NullLogger.Instance, cancellationToken);

		(await dbContext.Set<OrganizationMembership>().AnyAsync(m => m.OrganizationId == organizationId, cancellationToken))
			.Should().BeTrue("the retried run should now succeed and backfill the organization");
		(await dbContext.Set<OrganizationMembershipBackfillState>().AnyAsync(cancellationToken))
			.Should().BeTrue();
	}

	private static async Task<DomainOrganizationId> SeedOrganizationWithoutMembershipRowsAsync(
		ApplicationDbContext dbContext, CancellationToken cancellationToken)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"Legacy Org {Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);
		await dbContext.SaveChangesAsync(cancellationToken);
		return organization.Id;
	}

	private sealed class FakeKeycloakOrganizationService : IKeycloakOrganizationService
	{
		public IReadOnlyList<KeycloakOrganizationMember> MembersToReturn { get; set; } = [];

		public bool ThrowOnGetMembers { get; set; }

		public int GetMembersCallCount { get; private set; }

		public int GetRealmOrganisatorCallCount { get; private set; }

		public Task<IReadOnlyList<KeycloakOrganizationMember>> GetMembersAsync(
			Guid organizationId, CancellationToken cancellationToken = default)
		{
			GetMembersCallCount++;

			if (ThrowOnGetMembers)
				throw new HttpRequestException("Keycloak unavailable (simulated).");

			return Task.FromResult(MembersToReturn);
		}

		public Task<IReadOnlySet<Guid>> GetRealmOrganisatorUserIdsAsync(CancellationToken cancellationToken = default)
		{
			GetRealmOrganisatorCallCount++;

			return Task.FromResult<IReadOnlySet<Guid>>(
				MembersToReturn.Where(m => m.IsOrganisator).Select(m => m.UserId).ToHashSet());
		}

		public Task<Guid> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<Guid?> FindOrganizationByNameAsync(string name, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task AddMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task DeleteOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task AssignOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task RevokeOrganizerRoleAsync(Guid userId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<IReadOnlyList<KeycloakOrganizationMember>> SearchUsersAsync(
			string search, int max = 20, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();
	}
}
