using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EngagementTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	// ── CreateEngagement ──────────────────────────────────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldReturn201_WhenVolunteerSignsUp(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var result = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = null, Message = "I want to help!" },
			cancellationToken);

		result.OpportunityId.Should().Be(opportunity.Id);
		result.Status.Should().Be("Pending");
	}

	[Test]
	public async Task CreateEngagement_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var anonClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task CreateEngagement_ShouldReturn404_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.CreateEngagementAsync(
			Guid.NewGuid(),
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── GetEngagements ────────────────────────────────────────────────────────

	[Test]
	public async Task GetEngagements_ShouldReturnSignedUpVolunteer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help" },
			cancellationToken);

		var engagements = await olafClient.GetEngagementsAsync(opportunity.Id, cancellationToken);

		engagements.Should().HaveCount(1);
		engagements.Single().Status.Should().Be("Pending");
	}

	[Test]
	public async Task GetEngagements_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var anonClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonClient.GetEngagementsAsync(Guid.NewGuid(), cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetEngagements_ShouldReturn403_WhenUserLacksOrganisatorRole(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.GetEngagementsAsync(Guid.NewGuid(), cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	// ── ConfirmEngagement ─────────────────────────────────────────────────────

	[Test]
	public async Task ConfirmEngagement_ShouldReturnConfirmedStatus_WhenOrganisatorConfirms(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var result = await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		result.Status.Should().Be("Confirmed");
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn403_WhenNonOrganisatorConfirms(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// vera is not an organisator - she cannot confirm engagements
		var act = () => veraClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn404_WhenEngagementDoesNotExist(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		await CreateOrganizationAsync(olafClient, cancellationToken);

		var act = () => olafClient.ConfirmEngagementAsync(Guid.NewGuid(), cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── CancelEngagement ──────────────────────────────────────────────────────

	[Test]
	public async Task CancelEngagement_ShouldReturnCancelledStatus_WhenOrganisatorCancels(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var result = await olafClient.CancelEngagementAsync(
			engagement.Id,
			new CancelEngagementRequest { Reason = "Event cancelled" },
			cancellationToken);

		result.Status.Should().Be("Cancelled");
		result.CancellationReason.Should().Be("Event cancelled");
	}

	[Test]
	public async Task CancelEngagement_ShouldReturn403_WhenNonOrganisatorCancels(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var act = () => veraClient.CancelEngagementAsync(engagement.Id, null, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	// ── WithdrawEngagement ────────────────────────────────────────────────────

	[Test]
	public async Task WithdrawEngagement_ShouldReturnWithdrawnStatus_WhenVolunteerWithdraws(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var result = await veraClient.WithdrawEngagementAsync(engagement.Id, cancellationToken);

		result.Status.Should().Be("Withdrawn");
	}

	// ── GetMyEngagements ──────────────────────────────────────────────────────

	[Test]
	public async Task GetMyEngagements_ShouldReturnVolunteerEngagements(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var myEngagements = await veraClient.GetMyEngagementsAsync(cancellationToken);

		myEngagements.Should().HaveCount(1);
		myEngagements.Single().Status.Should().Be("Pending");
	}

	// ── Cross-org ownership ───────────────────────────────────────────────────

	[Test]
	public async Task GetEngagements_ShouldReturn403_WhenOrganisatorAccessesOtherOrgsOpportunity(
		CancellationToken cancellationToken)
	{
		// olaf creates org1 with an opportunity
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// vera creates her own org - this grants her the organisator role
		await CreateOrganizationAsync(veraClient, cancellationToken);

		// vera (organisator, but NOT in org1) tries to access org1's engagements
		var act = () => veraClient.GetEngagementsAsync(opportunity.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn403_WhenOrganisatorConfirmsOtherOrgsEngagement(
		CancellationToken cancellationToken)
	{
		// olaf creates org1 with an opportunity
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// vera creates her own org - this grants her the organisator role
		await CreateOrganizationAsync(veraClient, cancellationToken);

		// vera (organisator of org2, NOT org1) tries to confirm org1's engagement
		var act = () => veraClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

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
		var uniqueName = $"EngagementTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
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
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
			},
			cancellationToken);
	}
}
