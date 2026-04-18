using System.Net.Http.Headers;
using AwesomeAssertions;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class OrganizationSettingsTests(IntegrationTestFixture fixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── GetOrganizationDetails ──────────────────────────────────────────────

    [Fact]
    public async Task GetOrganizationDetails_ShouldReturnDetails_AfterCreation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Feuerwehr Details Test" }, ct);

        // Act
        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, ct);

        // Assert
        result.Id.Should().Be(created.Id.Value);
        result.Name.Should().Be("Feuerwehr Details Test");
        result.Members.Should().NotBeEmpty();
        result.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetOrganizationDetails_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpClient = fixture.Factory.CreateDefaultClient();
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), ct);

        // Assert
        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetOrganizationDetails_ShouldReturn403_WhenUserLacksOrganisatorRole()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("hannah", "hannah123", ct);

        // Act
        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), ct);

        // Assert
        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetOrganizationDetails_ShouldReturn404_WhenOrganizationDoesNotExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        // Act
        var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), ct);

        // Assert
        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(404);
    }

    // ── UpdateOrganization ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrganization_ShouldReturn204_WithValidData()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Vor Update" }, ct);

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

        // Act
        await client.UpdateOrganizationAsync(created.Id.Value, updateRequest, ct);

        // Assert — verify via GET
        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, ct);
        result.Name.Should().Be("Nach Update");
        result.Description.Should().Be("Neue Beschreibung");
        result.ContactEmail.Should().Be("kontakt@feuerwehr.de");
        result.Address.Should().NotBeNull();
        result.Address!.City.Should().Be("Berlin");
    }

    [Fact]
    public async Task UpdateOrganization_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpClient = fixture.Factory.CreateDefaultClient();
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.UpdateOrganizationAsync(
            Guid.NewGuid(),
            new UpdateOrganizationRequest { Name = "X" },
            ct);

        // Assert
        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task UpdateOrganization_ShouldClearAddress_WhenNullPassed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync("olaf", "olaf123", ct);

        var created = await client.CreateOrganizationAsync(
            new CreateOrganizationRequest { Name = "Org mit Adresse" }, ct);

        await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
        {
            Name = "Org mit Adresse",
            Address = new UpdateAddressRequest
            {
                Street = "Straße", HouseNumber = "1", ZipCode = "12345", City = "Stadt"
            }
        }, ct);

        // Act — remove address
        await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
        {
            Name = "Org mit Adresse",
            Address = null
        }, ct);

        // Assert
        var result = await client.GetOrganizationDetailsAsync(created.Id.Value, ct);
        result.Address.Should().BeNull();
    }

    // ── RemoveMember ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMember_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var httpClient = fixture.Factory.CreateDefaultClient();
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), ct);

        // Assert
        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(401);
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
