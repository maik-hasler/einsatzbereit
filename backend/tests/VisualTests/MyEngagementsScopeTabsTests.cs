using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #675: "My Profile -> Engagements" split into "Current &amp;
/// Upcoming" (default) and "Past" tabs, each paginated, instead of one
/// unbounded flat list. A Pending engagement should only show up under
/// "Current &amp; Upcoming", and a Withdrawn one only under "Past".
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeTabsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementsTab_SplitsPendingAndWithdrawn_AcrossUpcomingAndPastScopes()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var upcomingOpportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsUpcoming");
		var pastOpportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsPast");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");

		var upcomingEngagementId = await ApplyAsync(veraHttp, upcomingOpportunityId, "Still pending.");
		var pastEngagementId = await ApplyAsync(veraHttp, pastOpportunityId, "About to withdraw.");

		var withdrawResponse = await veraHttp.PostAsync($"/v1/engagements/{pastEngagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Default "Current & Upcoming" scope: the still-pending engagement is
		// visible, the withdrawn one is not.
		await Expect(Page.GetByText("ScopeTabsUpcoming").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("ScopeTabsPast")).Not.ToBeVisibleAsync();

		// Switching to "Past" flips which one is visible.
		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();
		await Expect(Page.GetByText("ScopeTabsPast").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("ScopeTabsUpcoming")).Not.ToBeVisibleAsync();

		// Leave vera's account clean for the rest of this shared Aspire session.
		var cleanupResponse = await veraHttp.PostAsync($"/v1/engagements/{upcomingEngagementId}/withdraw", content: null);
		cleanupResponse.EnsureSuccessStatusCode();
	}

	private static async Task<string> ApplyAsync(HttpClient http, string opportunityId, string message)
	{
		var response = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message });
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
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

	private static async Task<string> CreateIndividualContactOpportunityAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs (see EngagementReactivationTests).
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"MyEngagementsScopeTabs Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"{label} {suffix}",
			description = "Created by MyEngagementsScopeTabsTests",
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
		return opportunity.GetProperty("id").GetString()!;
	}
}
