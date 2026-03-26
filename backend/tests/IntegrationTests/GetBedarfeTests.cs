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
        var result = await sut.V1BedarfeGetAsync(1, 10, ct);

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
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 1",
            Description = "Beschreibung 1"
        }, ct);
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 2",
            Description = "Beschreibung 2"
        }, ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.V1BedarfeGetAsync(1, 10, ct);

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
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 1",
            Description = "Beschreibung 1"
        }, ct);
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 2",
            Description = "Beschreibung 2"
        }, ct);
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 3",
            Description = "Beschreibung 3"
        }, ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.V1BedarfeGetAsync(1, 2, ct);

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
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 1",
            Description = "Beschreibung 1"
        }, ct);
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 2",
            Description = "Beschreibung 2"
        }, ct);
        await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Bedarf 3",
            Description = "Beschreibung 3"
        }, ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.V1BedarfeGetAsync(2, 2, ct);

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

        var first = await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Erster Bedarf",
            Description = "Wurde zuerst erstellt"
        }, ct);

        var second = await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Zweiter Bedarf",
            Description = "Wurde als zweites erstellt"
        }, ct);

        var third = await authenticatedClient.V1BedarfePostAsync(new CreateBedarfRequest
        {
            Title = "Dritter Bedarf",
            Description = "Wurde zuletzt erstellt"
        }, ct);

        var httpClient = fixture.Factory.CreateDefaultClient();
        var sut = new EinsatzbereitApi(httpClient);

        // Act
        var result = await sut.V1BedarfeGetAsync(1, 10, ct);

        // Assert
        var items = result.Items.ToList();
        items.Should().HaveCount(3);
        items[0].Id.Value.Should().Be(third.Id.Value);
        items[1].Id.Value.Should().Be(second.Id.Value);
        items[2].Id.Value.Should().Be(first.Id.Value);
    }

    private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var token = await fixture.GetAccessTokenAsync("testuser", "testpassword");
        var httpClient = fixture.Factory.CreateDefaultClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return new EinsatzbereitApi(httpClient);
    }
}
