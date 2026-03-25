using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class AuthorizationTests(
    IntegrationTestFixture fixture)
    : IDisposable
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task MeEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var response = await _client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeEndpoint_ReturnsOk_WithValidToken()
    {
        var token = await fixture.GetAccessTokenAsync("testuser", "testpassword");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("testuser", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AdminEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var response = await _client.GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_ReturnsForbidden_WithNonAdminToken()
    {
        var token = await fixture.GetAccessTokenAsync("testuser", "testpassword");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_ReturnsOk_WithAdminToken()
    {
        var token = await fixture.GetAccessTokenAsync("adminuser", "adminpassword");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Admin access granted", json.GetProperty("message").GetString());
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
