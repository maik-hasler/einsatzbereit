using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AccountDeletionTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteMyAccount_ShouldRemoveAccountAcrossAllSubsystems_AndAnonymizeButNotDeleteEngagementHistory(
		CancellationToken cancellationToken)
	{
		// #829: DeleteMyAccount is irreversible and spans four subsystems with no
		// rollback on partial failure, so this runs against a throwaway per-test
		// Keycloak user (never vera/olaf/admin - those are the shared seed
		// accounts for the whole PerTestSession) since deleting it destroys the
		// account for good.
		var (ephemeralUserId, ephemeralUsername, ephemeralPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Account Deletion Test Org" }, cancellationToken);
		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Account Deletion Test Opportunity",
				Description = "Integration test opportunity for account deletion",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
			},
			cancellationToken);

		var engagement = await ephemeralClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// Fires an EngagementConfirmed notification whose recipient is the
		// ephemeral user, giving it a notification row to prove gets hard-deleted.
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		await ephemeralClient.DeleteMyAccountAsync(cancellationToken);

		// Keycloak subsystem: the account can no longer authenticate at all.
		var reLogin = () => fixture.GetAccessTokenAsync(ephemeralUsername, ephemeralPassword);
		await reLogin.Should().ThrowAsync<Exception>();

		// Notifications subsystem: hard-deleted, not just marked read/orphaned.
		(await fixture.CountRowsWhereAsync("notification", "recipient_id", ephemeralUserId))
			.Should().Be(0);

		// Users subsystem: the local user row is hard-deleted.
		(await fixture.CountRowsWhereAsync("user", "id", ephemeralUserId))
			.Should().Be(0);

		// Engagements subsystem: deliberately anonymized, not deleted - the
		// history survives for the organizer, but no longer identifies the
		// deleted volunteer.
		var engagements = await olafClient.GetEngagementsAsync(opportunity.Id, cancellationToken);
		engagements.Should().ContainSingle(e => e.Id == engagement.Id && e.VolunteerId == null);
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
