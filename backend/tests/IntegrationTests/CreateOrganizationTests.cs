using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class CreateOrganizationTests(
    IntegrationTestFixture fixture)
{
    [Test]
    public async Task CreateOrganization_ShouldReturnOrganization_WhenNameIsValid(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Feuerwehr Musterstadt" }, cancellationToken);

        result.Name.Should().Be("Feuerwehr Musterstadt");
    }

    [Test]
    public async Task CreateOrganization_ShouldSucceed_WhenNameContainsGermanCharacters(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Ärztlicher Übungsdienst Straße" }, cancellationToken);

        result.Name.Should().Be("Ärztlicher Übungsdienst Straße");
    }

    [Test]
    public async Task CreateOrganization_ShouldSucceed_WhenNameContainsSpecialCharacters(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var result = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Org (Test) & Co. #1" }, cancellationToken);

        result.Name.Should().Be("Org (Test) & Co. #1");
    }

    [Test]
    public async Task CreateOrganization_ShouldReturn401_WhenNotAuthenticated(
        CancellationToken cancellationToken)
    {
        var client = new EinsatzbereitApi(fixture.CreateHttpClient());

        var act = () => client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Unauthorized Org" }, cancellationToken);

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task GetOrganizations_ShouldReturnCreatedOrganization(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Testorganisation" }, cancellationToken);

        var result = await client.GetOrganizationsAsync(cancellationToken);

        result.Should().Contain(o => o.Name == "Testorganisation");
    }

    [Test]
    public async Task GetOrganizations_ShouldReturnEmpty_WhenUserHasNoOrganizations(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("hannah", "hannah123");

        var result = await client.GetOrganizationsAsync(cancellationToken);

        result.Should().BeEmpty();
    }

    private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
    {
        var token = await fixture.GetAccessTokenAsync(username, password);
        var httpClient = fixture.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return new EinsatzbereitApi(httpClient);
    }
}
