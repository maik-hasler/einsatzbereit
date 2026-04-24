using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class GetVolunteerOpportunitiesTests(IntegrationTestFixture fixture) : IAsyncInitializer
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnEmptyPagedList_WhenNoneExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        result.TotalItems.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.CurrentPage.Should().Be(1);
        result.PageCount.Should().Be(0);
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnAll_WhenOpportunitiesExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        result.TotalItems.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnCorrectPageSize_WhenPaginationIsApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 2, null, null, null, null, ct);

        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.PageCount.Should().Be(2);
        result.CurrentPage.Should().Be(1);
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnRemainingItems_WhenRequestingLastPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(2, 2, null, null, null, null, ct);

        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(1);
        result.CurrentPage.Should().Be(2);
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnOrderedByCreatedOnDescending()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        var first = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "First", "Created first", ct);
        var second = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Second", "Created second", ct);
        var third = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Third", "Created last", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        var items = result.Items.ToList();
        items.Should().HaveCount(3);
        items[0].Id.Should().Be(third.Id);
        items[1].Id.Should().Be(second.Id);
        items[2].Id.Should().Be(first.Id);
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnOrganizationName()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        var item = result.Items.Single();
        item.OrganizationName.Should().Contain("Testorg_");
    }

    [Test]
    public async Task GetVolunteerOpportunities_ShouldReturnAddressAndOccurrence()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", ct);

        var sut = new EinsatzbereitApi(fixture.CreateHttpClient());

        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        var item = result.Items.Single();
        item.Street.Should().Be("Musterstraße");
        item.HouseNumber.Should().Be("1");
        item.ZipCode.Should().Be("12345");
        item.City.Should().Be("Berlin");
        item.Occurrence.Should().Be("OneTime");
        item.ParticipationType.Should().Be("Waitlist");
        item.IsRemote.Should().BeFalse();
    }

    [Test]
    public async Task CreateVolunteerOpportunity_ShouldReturn403_WhenUserIsNotOrganizer()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = await fixture.GetAccessTokenAsync("hannah", "hannah123");
        var httpClient = fixture.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var client = new EinsatzbereitApi(httpClient);

        var act = () => client.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
        {
            Title = "Not allowed",
            Description = "Hannah cannot create opportunities",
            OrganizationId = Guid.NewGuid(),
            Street = "Straße",
            HouseNumber = "1",
            ZipCode = "12345",
            City = "Berlin",
            Occurrence = "OneTime",
            ParticipationType = "Waitlist"
        }, ct);

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task CreateVolunteerOpportunity_ShouldPersistAddressAndOccurrence()
    {
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        var result = await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
        {
            Title = "Opportunity with address",
            Description = "Test",
            OrganizationId = orgId,
            Street = "Hauptstraße",
            HouseNumber = "42a",
            ZipCode = "54321",
            City = "München",
            Occurrence = "Recurring",
            ParticipationType = "IndividualContact"
        }, ct);

        result.Street.Should().Be("Hauptstraße");
        result.HouseNumber.Should().Be("42a");
        result.ZipCode.Should().Be("54321");
        result.City.Should().Be("München");
        result.Occurrence.Should().Be("Recurring");
        result.ParticipationType.Should().Be("IndividualContact");
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
            Street = "Musterstraße",
            HouseNumber = "1",
            ZipCode = "12345",
            City = "Berlin",
            Occurrence = "OneTime",
            ParticipationType = "Waitlist"
        }, cancellationToken);
    }
}
