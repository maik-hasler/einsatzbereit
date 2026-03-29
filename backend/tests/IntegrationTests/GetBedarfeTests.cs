using System.Net.Http.Headers;
using AwesomeAssertions;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class GetBedarfeTests(IntegrationTestFixture fixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetBedarfe_ShouldReturnEmptyPagedList_WhenNoBedarfeExist()
    {
        // Arrange
        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await sut.GetBedarfeAsync(1, 10, ct);

        // Assert
        result.TotalItems.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.CurrentPage.Should().Be(1);
        result.PageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnAllBedarfe_WhenBedarfeExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 1", "Beschreibung 1", ct);
        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 2", "Beschreibung 2", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(1, 10, ct);

        // Assert
        result.TotalItems.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnCorrectPageSize_WhenPaginationIsApplied()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 1", "Beschreibung 1", ct);
        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 2", "Beschreibung 2", ct);
        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 3", "Beschreibung 3", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(1, 2, ct);

        // Assert
        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.PageCount.Should().Be(2);
        result.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnRemainingItems_WhenRequestingLastPage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 1", "Beschreibung 1", ct);
        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 2", "Beschreibung 2", ct);
        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf 3", "Beschreibung 3", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(2, 2, ct);

        // Assert
        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(1);
        result.CurrentPage.Should().Be(2);
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnBedarfeOrderedByCreatedOnDescending_WhenMultipleBedarfeExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        var first = await CreateBedarfAsync(authenticatedClient, orgId, "Erster Bedarf", "Wurde zuerst erstellt", ct);
        var second = await CreateBedarfAsync(authenticatedClient, orgId, "Zweiter Bedarf", "Wurde als zweites erstellt", ct);
        var third = await CreateBedarfAsync(authenticatedClient, orgId, "Dritter Bedarf", "Wurde zuletzt erstellt", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(1, 10, ct);

        // Assert
        var items = result.Items.ToList();
        items.Should().HaveCount(3);
        items[0].Id.Should().Be(third.Id);
        items[1].Id.Should().Be(second.Id);
        items[2].Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnOrganisationName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf", "Beschreibung", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(1, 10, ct);

        // Assert
        var item = result.Items.Single();
        item.OrganisationName.Should().Contain("Testorganisation");
    }

    [Fact]
    public async Task GetBedarfe_ShouldReturnAdresseAndFrequenz()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        await CreateBedarfAsync(authenticatedClient, orgId, "Bedarf", "Beschreibung", ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.GetBedarfeAsync(1, 10, ct);

        // Assert
        var item = result.Items.Single();
        item.Adresse.Strasse.Should().Be("Musterstraße");
        item.Adresse.Hausnummer.Should().Be("1");
        item.Adresse.Plz.Should().Be("12345");
        item.Adresse.Ort.Should().Be("Berlin");
        item.Status.Should().Be("Entwurf");
    }

    [Fact]
    public async Task CreateBedarf_ShouldReturn403_WhenUserIsNotOrganisator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var token = await fixture.GetAccessTokenAsync("hannah", "hannah123");
        var httpClient = fixture.Factory.CreateDefaultClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var client = new EinsatzbereitApi(httpClient);

        // Act
        var act = () => client.CreateBedarfAsync(new CreateBedarfRequest
        {
            Title = "Nicht erlaubt",
            Description = "Hannah darf keine Bedarfe erstellen",
            OrganisationId = Guid.NewGuid(),
            Strasse = "Straße",
            Hausnummer = "1",
            Plz = "12345",
            Ort = "Berlin",
            Frequenz = "Einmalig"
        }, ct);

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateBedarf_ShouldPersistAddressAndFrequenz()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var authenticatedClient = await CreateAuthenticatedClientAsync(ct);
        var orgId = await CreateOrganisationAsync(authenticatedClient, ct);

        // Act
        var result = await authenticatedClient.CreateBedarfAsync(new CreateBedarfRequest
        {
            Title = "Bedarf mit Adresse",
            Description = "Test",
            OrganisationId = orgId,
            Strasse = "Hauptstraße",
            Hausnummer = "42a",
            Plz = "54321",
            Ort = "München",
            Frequenz = "Regelmaessig"
        }, ct);

        // Assert
        result.Strasse.Should().Be("Hauptstraße");
        result.Hausnummer.Should().Be("42a");
        result.Plz.Should().Be("54321");
        result.Ort.Should().Be("München");
        result.Frequenz.Should().Be("Regelmaessig");
        result.Status.Should().Be("Entwurf");
    }

    private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
        var httpClient = fixture.Factory.CreateDefaultClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return new EinsatzbereitApi(httpClient);
    }

    private static async Task<Guid> CreateOrganisationAsync(
        EinsatzbereitApi client, CancellationToken cancellationToken)
    {
        var uniqueName = $"Testorg_{Guid.NewGuid()}"; // Jedes Mal ein neuer Name
        var organisation = await client.CreateOrganisationAsync(
            new CreateOrganisationRequest { Name = uniqueName }, cancellationToken);
        return organisation.Id.Value;
    }

    private static async Task<CreateBedarfResponse> CreateBedarfAsync(
        EinsatzbereitApi client, Guid orgId, string title, string description,
        CancellationToken cancellationToken)
    {
        return await client.CreateBedarfAsync(new CreateBedarfRequest
        {
            Title = title,
            Description = description,
            OrganisationId = orgId,
            Strasse = "Musterstraße",
            Hausnummer = "1",
            Plz = "12345",
            Ort = "Berlin",
            Frequenz = "Einmalig"
        }, cancellationToken);
    }
}
