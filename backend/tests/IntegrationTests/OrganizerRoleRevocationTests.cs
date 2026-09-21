using AwesomeAssertions;

namespace IntegrationTests;

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
		// Arrange

		var (soleOrganizerId, soleOrganizerUsername, soleOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var (coOrganizerId, coOrganizerUsername, coOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);

		var soleOrganizerClient = await fixture.CreateAuthenticatedClientAsync(soleOrganizerUsername, soleOrganizerPassword);
		var org = await soleOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Sole Organizer Removal {Guid.NewGuid()}" }, cancellationToken);

		soleOrganizerClient = await fixture.CreateAuthenticatedClientAsync(soleOrganizerUsername, soleOrganizerPassword);
		var invitation = await soleOrganizerClient.CreateInvitationAsync(
			org.Id.Value,
			new CreateInvitationRequest { InviteeId = coOrganizerId, Role = "Organizer" },
			cancellationToken);

		var coOrganizerClient = await fixture.CreateAuthenticatedClientAsync(coOrganizerUsername, coOrganizerPassword);
		await coOrganizerClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// Act

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
		var client = await fixture.CreateAuthenticatedClientAsync(username, password);

		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Sole Organizer Deletion {Guid.NewGuid()}" }, cancellationToken);

		client = await fixture.CreateAuthenticatedClientAsync(username, password);

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
		// Arrange

		var (twoOrgOrganizerId, twoOrgOrganizerUsername, twoOrgOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var (coOrganizerId, coOrganizerUsername, coOrganizerPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);

		var twoOrgOrganizerClient = await fixture.CreateAuthenticatedClientAsync(twoOrgOrganizerUsername, twoOrgOrganizerPassword);
		var orgA = await twoOrgOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Two Org Organizer A {Guid.NewGuid()}" }, cancellationToken);
		var orgB = await twoOrgOrganizerClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Two Org Organizer B {Guid.NewGuid()}" }, cancellationToken);

		twoOrgOrganizerClient = await fixture.CreateAuthenticatedClientAsync(twoOrgOrganizerUsername, twoOrgOrganizerPassword);
		var invitation = await twoOrgOrganizerClient.CreateInvitationAsync(
			orgA.Id.Value,
			new CreateInvitationRequest { InviteeId = coOrganizerId, Role = "Organizer" },
			cancellationToken);

		var coOrganizerClient = await fixture.CreateAuthenticatedClientAsync(coOrganizerUsername, coOrganizerPassword);
		await coOrganizerClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// Act
		await coOrganizerClient.RemoveMemberAsync(orgA.Id.Value, twoOrgOrganizerId, cancellationToken);

		// Assert
		(await fixture.UserHasOrganisatorRoleAsync(twoOrgOrganizerId, cancellationToken))
			.Should().BeTrue();

		var orgBDetails = await twoOrgOrganizerClient.GetOrganizationDetailsAsync(orgB.Id.Value, cancellationToken);
		orgBDetails.Members.Should().ContainSingle(m => m.UserId == twoOrgOrganizerId);
	}
}
