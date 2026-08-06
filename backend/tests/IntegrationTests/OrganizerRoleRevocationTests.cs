using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Coverage for #1677 bug 3: the realm-wide "organisator" Keycloak role was
// never revoked when a user left their only organization (RemoveMember) or
// deleted their only organization (DeleteOrganization) - only
// ChangeMemberRole's demotion path did this correctly. See
// ChangeMemberRoleCommandHandler's own comment for why the role is realm-wide
// rather than per-organization (#1386).
//
// All three tests use ephemeral users (never olaf/vera/admin): revoking the
// shared BaselineOrganisator's role would not be restored by
// ResetKeycloakOrganisatorRolesAsync between tests (it only ever *skips
// deleting* olaf's role, never re-grants it), so it would leak into every
// later test in this shared session that assumes olaf already organizes
// something - the same reasoning documented on
// IntegrationTestFixture.CreateEphemeralUserAsync.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizerRoleRevocationTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task RemoveMember_ShouldRevokeOrganisatorRole_WhenRemovedOrganizerHasNoOtherOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange - soleOrganizer creates the org (becoming its Organizer);
		// coOrganizer is invited in as a second Organizer purely so someone else
		// is allowed to remove soleOrganizer - RemoveMemberCommandHandler rejects
		// removing an organization's last remaining organizer, self-removal or
		// not.
		var (soleOrganizerId, soleOrganizerUsername, soleOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var (coOrganizerId, coOrganizerUsername, coOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);

		var soleOrganizerClient = await CreateAuthenticatedClientAsync(soleOrganizerUsername, soleOrganizerPassword);
		var org = await soleOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Sole Organizer Removal {Guid.NewGuid()}" }, cancellationToken);

		// A fresh token is needed to invite: CreateInvitation is gated by the
		// Organisator policy, a role claim baked into the JWT at mint time -
		// soleOrganizerClient's original token predates the "organisator" role
		// grant that just happened as a side effect of creating the
		// organization (same reasoning as AccountDeletionTests).
		soleOrganizerClient = await CreateAuthenticatedClientAsync(soleOrganizerUsername, soleOrganizerPassword);
		var invitation = await soleOrganizerClient.CreateInvitationAsync(
			org.Id.Value,
			new CreateInvitationRequest { InviteeId = coOrganizerId, Role = "Organizer" },
			cancellationToken);

		var coOrganizerClient = await CreateAuthenticatedClientAsync(coOrganizerUsername, coOrganizerPassword);
		await coOrganizerClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// Act - the co-organizer removes the sole organizer from their only org.
		// RemoveMember only requires the DefaultUser policy at the HTTP layer (the
		// Organizer check happens against the DB inside the handler), so
		// coOrganizerClient's original token is fine here.
		await coOrganizerClient.RemoveMemberAsync(org.Id.Value, soleOrganizerId, cancellationToken);

		// Assert
		(await fixture.UserHasOrganisatorRoleAsync(soleOrganizerId, cancellationToken))
			.Should().BeFalse();
	}

	[Test]
	public async Task DeleteOrganization_ShouldRevokeOrganisatorRole_WhenSoleOrganizerDeletesTheirOnlyOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (userId, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var client = await CreateAuthenticatedClientAsync(username, password);

		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Sole Organizer Deletion {Guid.NewGuid()}" }, cancellationToken);

		// A fresh token is needed: DeleteOrganization is gated by the
		// Organisator policy, a role claim baked into the JWT at mint time.
		client = await CreateAuthenticatedClientAsync(username, password);

		// Act
		await client.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		// Assert
		(await fixture.UserHasOrganisatorRoleAsync(userId, cancellationToken))
			.Should().BeFalse();
	}

	[Test]
	public async Task RemoveMember_ShouldKeepOrganisatorRole_WhenRemovedOrganizerStillOrganizesAnotherOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange - twoOrgOrganizer organizes both orgA and orgB; coOrganizer is
		// invited into orgA only, purely so twoOrgOrganizer can be removed from
		// orgA without tripping the sole-organizer guard.
		var (twoOrgOrganizerId, twoOrgOrganizerUsername, twoOrgOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var (coOrganizerId, coOrganizerUsername, coOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);

		var twoOrgOrganizerClient = await CreateAuthenticatedClientAsync(twoOrgOrganizerUsername, twoOrgOrganizerPassword);
		var orgA = await twoOrgOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Two Org Organizer A {Guid.NewGuid()}" }, cancellationToken);
		var orgB = await twoOrgOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Two Org Organizer B {Guid.NewGuid()}" }, cancellationToken);

		// A fresh token is needed to invite (see above).
		twoOrgOrganizerClient = await CreateAuthenticatedClientAsync(twoOrgOrganizerUsername, twoOrgOrganizerPassword);
		var invitation = await twoOrgOrganizerClient.CreateInvitationAsync(
			orgA.Id.Value,
			new CreateInvitationRequest { InviteeId = coOrganizerId, Role = "Organizer" },
			cancellationToken);

		var coOrganizerClient = await CreateAuthenticatedClientAsync(coOrganizerUsername, coOrganizerPassword);
		await coOrganizerClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// Act - remove twoOrgOrganizer from orgA only; orgB is left untouched.
		await coOrganizerClient.RemoveMemberAsync(orgA.Id.Value, twoOrgOrganizerId, cancellationToken);

		// Assert - still Organizer of orgB, so the realm-wide role must stay.
		(await fixture.UserHasOrganisatorRoleAsync(twoOrgOrganizerId, cancellationToken))
			.Should().BeTrue();

		// Sanity check that orgB itself was untouched by the orgA removal.
		var orgBDetails = await twoOrgOrganizerClient.GetOrganizationDetailsAsync(orgB.Id.Value, cancellationToken);
		orgBDetails.Members.Should().ContainSingle(m => m.UserId == twoOrgOrganizerId);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
