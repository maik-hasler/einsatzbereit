using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class BedarfeTests(
    IntegrationTestFixture fixture)
    : IDisposable
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task ListBedarfe_ReturnsOk_WithoutAuthentication()
    {
        var response = await _client.GetAsync("/api/bedarfe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.True(json.GetArrayLength() > 0, "Seed-Daten sollten vorhanden sein");
    }

    [Fact]
    public async Task GetBedarf_ReturnsOk_ForExistingSeedData()
    {
        var listResponse = await _client.GetAsync("/api/bedarfe", TestContext.Current.CancellationToken);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var firstId = list[0].GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/bedarfe/{firstId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bedarf = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(bedarf.TryGetProperty("titel", out _));
        Assert.True(bedarf.TryGetProperty("beschreibung", out _));
        Assert.True(bedarf.TryGetProperty("ort", out _));
        Assert.True(bedarf.TryGetProperty("organisation", out _));
    }

    [Fact]
    public async Task GetBedarf_ReturnsNotFound_ForNonExistentId()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/bedarfe/{nonExistentId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
