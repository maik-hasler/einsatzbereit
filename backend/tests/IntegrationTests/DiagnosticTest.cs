using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class DiagnosticTest(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Diagnostic_TestAddMemberFormats()
    {
        var ct = TestContext.Current.CancellationToken;
        var keycloakBase = fixture.KeycloakBaseAddress;

        using var adminClient = new HttpClient();

        // Get service account token
        var tokenResponse = await adminClient.PostAsync(
            $"{keycloakBase}realms/einsatzbereit/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "backend",
                ["client_secret"] = "backend-secret"
            }), ct);
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var adminToken = tokenJson.GetProperty("access_token").GetString()!;
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Look up olaf
        var usersResponse = await adminClient.GetAsync(
            $"{keycloakBase}admin/realms/einsatzbereit/users?username=olaf&exact=true", ct);
        var users = await usersResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var userId = users[0].GetProperty("id").GetString()!;
        var fullUser = users[0].GetRawText();

        // Create orgs for each test
        var results = new List<string>();

        // Test 1: POST /members with full user representation
        var orgId1 = await CreateOrgAsync(adminClient, keycloakBase, "diag-1", ct);
        var r1 = await adminClient.PostAsync(
            $"{keycloakBase}admin/realms/einsatzbereit/organizations/{orgId1}/members",
            new StringContent(fullUser, Encoding.UTF8, "application/json"), ct);
        results.Add($"Full user rep: {r1.StatusCode} - {await r1.Content.ReadAsStringAsync(ct)}");

        // Test 2: POST /members/{userId} (no body)
        var orgId2 = await CreateOrgAsync(adminClient, keycloakBase, "diag-2", ct);
        var r2 = await adminClient.PostAsync(
            $"{keycloakBase}admin/realms/einsatzbereit/organizations/{orgId2}/members/{userId}",
            null, ct);
        results.Add($"POST /members/{{userId}}: {r2.StatusCode} - {await r2.Content.ReadAsStringAsync(ct)}");

        // Test 3: PUT /members/{userId}
        var orgId3 = await CreateOrgAsync(adminClient, keycloakBase, "diag-3", ct);
        var r3 = await adminClient.PutAsync(
            $"{keycloakBase}admin/realms/einsatzbereit/organizations/{orgId3}/members/{userId}",
            null, ct);
        results.Add($"PUT /members/{{userId}}: {r3.StatusCode} - {await r3.Content.ReadAsStringAsync(ct)}");

        Assert.Fail($"userId: {userId}\n{string.Join("\n", results)}");
    }

    private static async Task<string> CreateOrgAsync(
        HttpClient client, string keycloakBase, string name, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            $"{keycloakBase}admin/realms/einsatzbereit/organizations",
            new { name, alias = name }, ct);
        response.EnsureSuccessStatusCode();
        return response.Headers.Location!.ToString().Split('/')[^1];
    }
}
