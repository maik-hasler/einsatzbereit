using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizationOpportunitiesTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturnEmptyPagedList_WhenNoneExistForStatus(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		var result = await client.GetOrganizationOpportunitiesAsync(orgId, "Published", 1, 10, cancellationToken);

		result.TotalItems.Should().Be(0);
		result.Items.Should().BeEmpty();
		result.CurrentPage.Should().Be(1);
		result.PageCount.Should().Be(0);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturnOnlyDrafts_WhenStatusIsDraft(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		await CreateDraftOpportunityAsync(client, orgId, "Draft 1", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Published 1", cancellationToken);

		var result = await client.GetOrganizationOpportunitiesAsync(orgId, "Draft", 1, 10, cancellationToken);

		result.TotalItems.Should().Be(1);
		result.Items.Single().Title.Should().Be("Draft 1");
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturnOnlyPublished_WhenStatusIsPublished(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		await CreateDraftOpportunityAsync(client, orgId, "Draft 1", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Published 1", cancellationToken);

		var result = await client.GetOrganizationOpportunitiesAsync(orgId, "Published", 1, 10, cancellationToken);

		result.TotalItems.Should().Be(1);
		result.Items.Single().Title.Should().Be("Published 1");
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturnCorrectPageSize_WhenPaginationIsApplied(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 1", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 2", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 3", cancellationToken);

		var result = await client.GetOrganizationOpportunitiesAsync(orgId, "Published", 1, 2, cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(2);
		result.PageCount.Should().Be(2);
		result.CurrentPage.Should().Be(1);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturnRemainingItems_WhenRequestingLastPage(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 1", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 2", cancellationToken);
		await CreatePublishedOpportunityAsync(client, orgId, "Opportunity 3", cancellationToken);

		var result = await client.GetOrganizationOpportunitiesAsync(orgId, "Published", 2, 2, cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(1);
		result.CurrentPage.Should().Be(2);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturn400_WhenStatusIsInvalid(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		var act = () => client.GetOrganizationOpportunitiesAsync(orgId, "NotAStatus", 1, 10, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturn400_WhenPageNumberIsLessThanOne(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		var act = () => client.GetOrganizationOpportunitiesAsync(orgId, "Published", 0, 10, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturn400_WhenPageSizeIsOutOfRange(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(client, cancellationToken);

		var act = () => client.GetOrganizationOpportunitiesAsync(orgId, "Published", 1, 101, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var anonClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonClient.GetOrganizationOpportunitiesAsync(Guid.NewGuid(), "Published", 1, 10, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetOrganizationOpportunities_ShouldReturn403_WhenOrganisatorAccessesOtherOrgsOpportunities(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);

		var veraToken = await fixture.GetAccessTokenAsync("vera", "vera123");
		var veraHttpClient = fixture.CreateHttpClient();
		veraHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", veraToken);
		var veraClient = new EinsatzbereitApi(veraHttpClient);

		// vera creates her own org - this grants her the organisator role
		await CreateOrganizationAsync(veraClient, cancellationToken);

		// vera (organisator, but NOT in org1) tries to access org1's opportunities
		var act = () => veraClient.GetOrganizationOpportunitiesAsync(org1Id, "Published", 1, 10, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"OrgOpportunitiesTestOrg_{Guid.NewGuid()}";
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return organization.Id.Value;
	}

	private static Task<CreateVolunteerOpportunityResponse> CreateDraftOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, CancellationToken cancellationToken) =>
		client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = title,
			Description = "Integration test opportunity",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			IsDraft = true,
		}, cancellationToken);

	private static async Task<CreateVolunteerOpportunityResponse> CreatePublishedOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, CancellationToken cancellationToken)
	{
		var opportunity = await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = title,
			Description = "Integration test opportunity",
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
		}, cancellationToken);

		return opportunity;
	}
}
