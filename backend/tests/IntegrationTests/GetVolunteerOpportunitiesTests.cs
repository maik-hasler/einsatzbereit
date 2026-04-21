using System.Net.Http.Headers;
using AwesomeAssertions;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class GetVolunteerOpportunitiesTests(IntegrationTestFixture fixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnEmptyPagedList_WhenNoneExist()
    {
        // Arrange
        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        // Assert
        result.TotalItems.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.CurrentPage.Should().Be(1);
        result.PageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnAll_WhenOpportunitiesExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        // Assert
        result.TotalItems.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnCorrectPageSize_WhenPaginationIsApplied()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 2, null, null, null, null, ct);

        // Assert
        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.PageCount.Should().Be(2);
        result.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnRemainingItems_WhenRequestingLastPage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 1", "Description 1", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 2", "Description 2", ct);
        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity 3", "Description 3", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(2, 2, null, null, null, null, ct);

        // Assert
        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(1);
        result.CurrentPage.Should().Be(2);
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnOrderedByCreatedOnDescending()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        var first = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "First", "Created first", ct);
        var second = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Second", "Created second", ct);
        var third = await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Third", "Created last", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        // Assert
        var items = result.Items.ToList();
        items.Should().HaveCount(3);
        items[0].Id.Should().Be(third.Id);
        items[1].Id.Should().Be(second.Id);
        items[2].Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnOrganizationName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        // Assert
        var item = result.Items.Single();
        item.OrganizationName.Should().Contain("Testorg_");
    }

    [Fact]
    public async Task GetVolunteerOpportunities_ShouldReturnAddressAndOccurrence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        await CreateVolunteerOpportunityAsync(authenticatedClient, orgId, "Opportunity", "Description", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetVolunteerOpportunitiesAsync(1, 10, null, null, null, null, ct);

        // Assert
        var item = result.Items.Single();
        item.Street.Should().Be("Musterstraße");
        item.HouseNumber.Should().Be("1");
        item.ZipCode.Should().Be("12345");
        item.City.Should().Be("Berlin");
        item.Occurrence.Should().Be("OneTime");
        item.ParticipationType.Should().Be("Waitlist");
        item.IsRemote.Should().BeFalse();
    }

    [Fact]
    public async Task CreateVolunteerOpportunity_ShouldReturn403_WhenUserIsNotOrganizer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var token = await fixture.GetAccessTokenAsync("hannah", "hannah123");
        var httpClient = fixture.Factory.CreateDefaultClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var client = new EinsatzbereitApi(httpClient);

        // Act
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

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateVolunteerOpportunity_ShouldPersistAddressAndOccurrence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganizationAsync(authenticatedClient, ct);

        // Act
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

        // Assert
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
        var httpClient = fixture.Factory.CreateDefaultClient();
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
