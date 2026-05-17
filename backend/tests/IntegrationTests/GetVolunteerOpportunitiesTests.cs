using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetVolunteerOpportunitiesTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnEmptyPagedList_WhenNoneExist(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(0);
		result.Items.Should().BeEmpty();
		result.CurrentPage.Should().Be(1);
		result.PageCount.Should().Be(0);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnAll_WhenOpportunitiesExist(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(2);
		result.Items.Should().HaveCount(2);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnCorrectPageSize_WhenPaginationIsApplied(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(2);
		result.PageCount.Should().Be(2);
		result.CurrentPage.Should().Be(1);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnRemainingItems_WhenRequestingLastPage(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(2, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(1);
		result.CurrentPage.Should().Be(2);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnOrderedByCreatedOnDescending(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var first = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "First", "Created first", cancellationToken);
		var second = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Second", "Created second", cancellationToken);
		var third = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Third", "Created last", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var items = result.Items.ToList();
		items.Should().HaveCount(3);
		items[0].Id.Should().Be(third.Id);
		items[1].Id.Should().Be(second.Id);
		items[2].Id.Should().Be(first.Id);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnOrganizationName(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.OrganizationName.Should().Contain("Testorg_");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnAddressAndOccurrence(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, cancellationToken: cancellationToken);

		var item = result.Items.Single();
		item.Street.Should().Be("Sample Street");
		item.HouseNumber.Should().Be("1");
		item.ZipCode.Should().Be("12345");
		item.City.Should().Be("Berlin");
		item.Occurrence.Should().Be("OneTime");
		item.ParticipationType.Should().Be("Waitlist");
		item.IsRemote.Should().BeFalse();
	}

	[Test]
	public async Task CreateVolunteerOpportunity_ShouldReturn403_WhenUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("vera", "vera123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		var client = new EinsatzbereitApi(httpClient);

		var act = () => client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Not allowed",
			Description = "Vera cannot create opportunities",
			OrganizationId = Guid.NewGuid(),
			Street = "Test Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "Waitlist",
			CheckInMethod = "None"
		}, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CreateVolunteerOpportunity_ShouldPersistAddressAndOccurrence(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		var result = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Opportunity with address",
			Description = "Test",
			OrganizationId = orgId,
			Street = "Main Street",
			HouseNumber = "42a",
			ZipCode = "54321",
			City = "Munich",
			Occurrence = "Recurring",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None"
		}, cancellationToken);

		result.Street.Should().Be("Main Street");
		result.HouseNumber.Should().Be("42a");
		result.ZipCode.Should().Be("54321");
		result.City.Should().Be("Munich");
		result.Occurrence.Should().Be("Recurring");
		result.ParticipationType.Should().Be("IndividualContact");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldFilterByKeyword(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Beach cleanup", "Collect litter", cancellationToken);
		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Soup kitchen", "Serve meals", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, "beach", cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle();
		result.Items.Single().Title.Should().Be("Beach cleanup");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldFilterByIsRemote(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, cancellationToken);

		await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "On-site task", "Description", cancellationToken);

		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var onSite = await sut.GetVolunteerOpportunitiesAsync(1, 10, isRemote: false, cancellationToken: cancellationToken);
		var remote = await sut.GetVolunteerOpportunitiesAsync(1, 10, isRemote: true, cancellationToken: cancellationToken);

		onSite.TotalItems.Should().Be(1);
		remote.TotalItems.Should().Be(0);
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturnBadRequest_WhenRadiusIsNotPositive(
		CancellationToken cancellationToken)
	{
		var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => sut.GetVolunteerOpportunitiesAsync(
			1, 10, centerLatitude: 52.5, centerLongitude: 13.4, radiusKm: 0, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
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
		var uniqueName = $"Testorg_{Guid.NewGuid()}";
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return organization.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateVolunteerOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, string description,
		CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = title,
			Description = description,
			OrganizationId = orgId,
			Street = "Sample Street",
			HouseNumber = "1",
			ZipCode = "12345",
			City = "Berlin",
			Occurrence = "OneTime",
			ParticipationType = "Waitlist",
			CheckInMethod = "None"
		}, cancellationToken);
	}
}
