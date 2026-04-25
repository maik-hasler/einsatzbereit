using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizationSettingsTests(
    IntegrationTestFixture fixture)
{
    [Before(Test)]
    public Task ResetAsync() => fixture.ResetAsync();

    [Test]
    public async Task GetOrganizationDetails_ShouldReturnDetails_AfterCreation(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Feuerwehr Details Test" }, cancellationToken);

        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);

        result.Id.Should().Be(created.Id.Value);
        result.Name.Should().Be("Feuerwehr Details Test");
        result.Members.Should().NotBeEmpty();
        result.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task GetOrganizationDetails_ShouldReturn401_WhenNotAuthenticated(
        CancellationToken cancellationToken)
    {
        var client = new EinsatzbereitApi(fixture.CreateHttpClient());

        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task GetOrganizationDetails_ShouldReturn403_WhenUserLacksOrganisatorRole(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("hannah", "hannah123");

        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task GetOrganizationDetails_ShouldReturn404_WhenOrganizationDoesNotExist(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(404);
    }

    // ── UpdateOrganization ──────────────────────────────────────────────────

    [Test]
    public async Task UpdateOrganization_ShouldReturn204_WithValidData(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Vor Update" }, cancellationToken);

        var updateRequest = new UpdateOrganizationRequest
        {
            Name = "Nach Update",
            Description = "Neue Beschreibung",
            ContactEmail = "kontakt@feuerwehr.de",
            ContactPhone = "+49 30 123456",
            Website = "https://feuerwehr.de",
            Address = new UpdateAddressRequest
            {
                Street = "Feuerwehrstraße",
                HouseNumber = "1",
                ZipCode = "10115",
                City = "Berlin"
            }
        };

        await client.UpdateOrganizationAsync(created.Id.Value, updateRequest, cancellationToken);

        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
        result.Name.Should().Be("Nach Update");
        result.Description.Should().Be("Neue Beschreibung");
        result.ContactEmail.Should().Be("kontakt@feuerwehr.de");
        result.Address.Should().NotBeNull();
        result.Address!.City.Should().Be("Berlin");
    }

    [Test]
    public async Task UpdateOrganization_ShouldReturn401_WhenNotAuthenticated(
        CancellationToken cancellationToken)
    {
        var client = new EinsatzbereitApi(fixture.CreateHttpClient());

        var act = () => client.UpdateOrganizationAsync(
            Guid.NewGuid(),
            new UpdateOrganizationRequest { Name = "X" },
            cancellationToken);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task UpdateOrganization_ShouldClearAddress_WhenNullPassed(
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Org mit Adresse" }, cancellationToken);

        await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
        {
            Name = "Org mit Adresse",
            Address = new UpdateAddressRequest
            {
                Street = "Straße", HouseNumber = "1", ZipCode = "12345", City = "Stadt"
            }
        }, cancellationToken);

        await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
        {
            Name = "Org mit Adresse",
            Address = null
        }, cancellationToken);

        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
        result.Address.Should().BeNull();
    }
    
    [Test]
    public async Task RemoveMember_ShouldReturn401_WhenNotAuthenticated(
        CancellationToken cancellationToken)
    {
        var client = new EinsatzbereitApi(fixture.CreateHttpClient());

        var act = () => client.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), cancellationToken);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
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
