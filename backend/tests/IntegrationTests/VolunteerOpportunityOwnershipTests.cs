using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Cross-org coverage for OwnershipGuard.EnsureIsOrganizerAsync on the volunteer-opportunity
// management endpoints (#1309) - each unit test suite already flips the guard to false, but
// these prove the same rejection happens end-to-end against a real second organization.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class VolunteerOpportunityOwnershipTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOpportunityCheckInPin_ShouldReturn403_WhenOrganizerAccessesOtherOrgsOpportunity(
		CancellationToken cancellationToken)
	{
		// olaf creates org1 with a PIN-protected opportunity
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken, checkInPin: "13579");

		// vera creates her own org - this grants her the organisator role, but not membership in org1
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await CreateOrganizationAsync(veraClient, cancellationToken);

		var act = () => veraClient.GetOpportunityCheckInPinAsync(opportunity.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task DeleteVolunteerOpportunity_ShouldReturn403_WhenOrganizerDeletesOtherOrgsOpportunity(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await CreateOrganizationAsync(veraClient, cancellationToken);

		var act = () => veraClient.DeleteVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);

		// The opportunity must still exist for its own org - deletion was rejected, not silently skipped.
		var stillExists = await olafClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);
		stillExists.Should().NotBeNull();
	}

	[Test]
	public async Task UpdateTimeSlot_ShouldReturn403_WhenOrganizerUpdatesOtherOrgsTimeSlot(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);
		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
			},
			cancellationToken);
		var timeSlotId = timeSlots.Single().Id;

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await CreateOrganizationAsync(veraClient, cancellationToken);

		var act = () => veraClient.UpdateTimeSlotAsync(
			opportunity.Id,
			timeSlotId,
			new UpdateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(14),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(14).AddHours(2),
				MaxParticipants = 99,
			},
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"OwnershipTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken, string? checkInPin = null)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Test Opportunity",
				Description = "Integration test opportunity",
				OrganizationId = orgId,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "Recurring",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = checkInPin is null ? "None" : "PINCode",
				CheckInPin = checkInPin,
				IsDraft = true,
			},
			cancellationToken);
	}
}
