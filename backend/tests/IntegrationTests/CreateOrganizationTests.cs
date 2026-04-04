using System.Net.Http.Headers;
using AwesomeAssertions;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class CreateOrganizationTests(IntegrationTestFixture fixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateOrganization_ShouldReturnOrganization_WhenNameIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Feuerwehr Musterstadt" }, ct);

        // Assert
        result.Name.Should().Be("Feuerwehr Musterstadt");
    }

    [Fact]
    public async Task CreateOrganization_ShouldSucceed_WhenNameContainsGermanCharacters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Ärztlicher Übungsdienst Straße" }, ct);

        // Assert
        result.Name.Should().Be("Ärztlicher Übungsdienst Straße");
    }

    [Fact]
    public async Task CreateOrganization_ShouldSucceed_WhenNameContainsSpecialCharacters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Org (Test) & Co. #1" }, ct);

        // Assert
        result.Name.Should().Be("Org (Test) & Co. #1");
    }

    [Fact]
    public async Task CreateOrganization_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpClient = fixture.Factory.CreateDefaultClient();
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Unauthorized Org" }, ct);

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetOrganizations_ShouldReturnCreatedOrganization()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Testorganisation" }, ct);

        // Act
        var result = await client.GetOrganizationsAsync(ct);

        // Assert
        result.Should().Contain(o => o.Name == "Testorganisation");
    }

    [Fact]
    public async Task GetOrganizations_ShouldReturnEmpty_WhenUserHasNoOrganizations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("hannah", "hannah123", ct);

        // Act
        var result = await client.GetOrganizationsAsync(ct);

        // Assert
        result.Should().BeEmpty();
    }

    private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        var token = await fixture.GetAccessTokenAsync(username, password);
        var httpClient = fixture.Factory.CreateDefaultClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return new EinsatzbereitApi(httpClient);
    }
}
