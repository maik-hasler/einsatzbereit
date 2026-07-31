using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetVolunteerOpportunityDetailsTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetVolunteerOpportunityDetails_ShouldReturn404_WhenOpportunityIsDraftAndRequesterIsAnonymous(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateDraftOpportunityAsync(olafClient, orgId, cancellationToken);

		var anonymousClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonymousClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task GetVolunteerOpportunityDetails_ShouldReturn404_WhenOpportunityIsDraftAndRequesterIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateDraftOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task GetVolunteerOpportunityDetails_ShouldReturnDetails_WhenOpportunityIsDraftAndRequesterIsOrganizer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateDraftOpportunityAsync(olafClient, orgId, cancellationToken);

		var details = await olafClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);

		details.Should().NotBeNull();
		details.Status.Should().Be("Draft");
	}

	[Test]
	public async Task GetVolunteerOpportunityDetails_ShouldReturnDetails_WhenOpportunityIsPublishedAndRequesterIsAnonymous(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateDraftOpportunityAsync(olafClient, orgId, cancellationToken);
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var anonymousClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var details = await anonymousClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);

		details.Should().NotBeNull();
		details.Status.Should().Be("Published");
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

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"DetailsTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateDraftOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken) =>
		await client.CreateVolunteerOpportunityAsync(
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
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
				IsDraft = true,
			},
			cancellationToken);
}
