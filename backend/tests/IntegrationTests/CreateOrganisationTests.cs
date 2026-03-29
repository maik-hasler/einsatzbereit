using System.Net.Http.Headers;
using AwesomeAssertions;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class CreateOrganisationTests(IntegrationTestFixture fixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateOrganisation_ShouldReturnOrganisation_WhenNameIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = "Feuerwehr Musterstadt" }, ct);

        // Assert
        result.Name.Should().Be("Feuerwehr Musterstadt");
    }

    [Fact]
    public async Task CreateOrganisation_ShouldSucceed_WhenNameContainsGermanCharacters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = "Ärztlicher Übungsdienst Straße" }, ct);

        // Assert
        result.Name.Should().Be("Ärztlicher Übungsdienst Straße");
    }

    [Fact]
    public async Task CreateOrganisation_ShouldSucceed_WhenNameContainsSpecialCharacters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var result = await client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = "Org (Test) & Co. #1" }, ct);

        // Assert
        result.Name.Should().Be("Org (Test) & Co. #1");
    }

    [Fact]
    public async Task CreateOrganisation_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpClient = fixture.Factory.CreateDefaultClient();
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = "Unauthorized Org" }, ct);

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetOrganisationen_ShouldReturnCreatedOrganisation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        await client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = "Testorganisation" }, ct);

        // Act
        var result = await client.GetOrganisationenAsync(ct);

        // Assert
        result.Should().Contain(o => o.Name == "Testorganisation");
    }

    [Fact]
    public async Task GetOrganisationen_ShouldReturnEmpty_WhenUserHasNoOrganisations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("hannah", "hannah123", ct);

        // Act
        var result = await client.GetOrganisationenAsync(ct);

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
