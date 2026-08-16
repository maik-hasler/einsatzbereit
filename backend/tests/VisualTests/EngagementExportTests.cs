using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Coverage for #1949: the CSV engagement export (#1045, closed by #1656) was
/// removed four days later by #1834 for having zero end-to-end test coverage.
/// Rebuilt with this test this time - drives a real click on the "Export"
/// button on the engagement management page and asserts the downloaded file's
/// name and content, not just that the button/handler exist.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementExportTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_Export_DownloadsCsvWithConfirmedVolunteersRow()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "EngagementExport");
		await CreateAndConfirmVeraEngagementAsync(keycloak, backend, opportunityId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Expect(Page.GetByText("Vera Volunteer")).ToBeVisibleAsync();

		var download = await Page.RunAndWaitForDownloadAsync(
			async () => await Page.GetByRole(AriaRole.Button, new() { Name = "Export" }).ClickAsync());

		download.SuggestedFilename.Should().Be($"engagements-{opportunityId}.csv");

		var path = await download.PathAsync();
		path.Should().NotBeNull();
		var content = await File.ReadAllTextAsync(path!);

		// Not asserting on the Status/header column text itself: it's localized
		// into whichever language olaf's (shared, session-wide) account happens
		// to have as its stored preference - see ExportEngagementsQueryHandlerTests
		// for deterministic coverage of both languages. Deliberately not setting
		// it here via PUT /v1/users/me either - that endpoint round-trips
		// firstName/lastName through Keycloak and blanks either one that isn't
		// also resent (KeycloakUserService.UpdateUserAsync), which would corrupt
		// this shared seeded account for every other test in the same session.
		//
		// Instead this proves the export mechanism itself end-to-end: the leading
		// "sep=;" directive (#1675), exactly one header row plus one data row (not
		// empty, not duplicated), and that row naming the confirmed volunteer.
		var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
		lines.Should().HaveCount(3);
		lines[0].Should().Be("sep=;");
		lines[2].Should().StartWith("Vera Volunteer;");
	}

	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}

	private static async Task<(string OpportunityId, string OrganizationId)> CreateIndividualContactOpportunityAsync(
		Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Fresh organization rather than olaf's shared seed org - see the
		// identical note in EngagementManagementCheckInPinTests.
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"{label} {suffix}",
			description = "Created by EngagementExportTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return (opportunity.GetProperty("id").GetString()!, organizationId);
	}

	private static async Task CreateAndConfirmVeraEngagementAsync(Uri keycloak, Uri backend, string opportunityId)
	{
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Export test signup" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		confirmResponse.EnsureSuccessStatusCode();
	}
}
