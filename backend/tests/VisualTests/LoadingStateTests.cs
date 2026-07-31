using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #765: several pages rendered bare, unstyled "Loading..."
/// text while fetching, with no visual sign anything was happening. These
/// tests delay the underlying API call so the loading state is observable
/// long enough to assert on it, then confirm real content replaces it.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LoadingStateTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_ShowsLoadingSkeleton_WhileOpportunitiesFetch()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.RouteAsync("**/v1/volunteer-opportunities?*", async route =>
		{
			if (route.Request.Method == "GET")
				await Task.Delay(1500);
			await route.ContinueAsync();
		});

		await Page.GotoAsync(frontend.ToString());

		// The skeleton's accessible name comes from a sr-only span; the
		// pulsing placeholder blocks themselves are aria-hidden. Scoped to
		// ":has(.animate-pulse)" rather than a bare "[role='status']" first
		// match, since routes are lazy-loaded (#1403): the route's own
		// Suspense fallback (a spinner, not a skeleton) is also role="status"
		// and briefly present while the page's JS chunk downloads, racing
		// this one for "first role='status' on the page".
		var loadingStatus = Page.Locator("[role='status']:has(.animate-pulse)").First;
		await Expect(loadingStatus).ToBeVisibleAsync();
		await Expect(loadingStatus).ToContainTextAsync("Loading");
		await Expect(loadingStatus.Locator(".animate-pulse")).Not.ToHaveCountAsync(0);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(loadingStatus).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.Locator("main")).ToBeVisibleAsync();
	}

	[Test]
	public async Task DetailPage_ShowsLoadingSkeleton_WhileOpportunityFetches()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync("LoadingSkeleton");
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method == "GET")
				await Task.Delay(1500);
			await route.ContinueAsync();
		});

		await Page.GotoAsync(detailUrl);

		// See the comment in HomePage_ShowsLoadingSkeleton_WhileOpportunitiesFetch
		// above on why this is scoped to ":has(.animate-pulse)".
		var loadingStatus = Page.Locator("[role='status']:has(.animate-pulse)").First;
		await Expect(loadingStatus).ToBeVisibleAsync();
		await Expect(loadingStatus).ToContainTextAsync("Loading");
		await Expect(loadingStatus.Locator(".animate-pulse")).Not.ToHaveCountAsync(0);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(loadingStatus).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();
	}

	[Test]
	public async Task AdministrationPage_ShowsSpinnerForOrganizationsSection_WhileOrganizationsFetch()
	{
		// Covers the shared Spinner component's contract (role="status", a
		// visible label, an aria-hidden spin icon) used by every other
		// converted loading spot in this diff (ProtectedRoute, OrgAppLayout,
		// EngagementManagementPage, etc.) - they all render the exact same
		// component, so exercising it once here is representative.
		//
		// /admin/organizations was consolidated into /administration (single
		// page, Organizations + Users sections stacked) after this test was
		// written, backed by a real admin-wide GET /v1/admin/organizations
		// instead of the old caller-scoped GET /v1/organizations - only the
		// route/URL changed here, the Spinner contract being tested has not.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");

		await Page.RouteAsync("**/v1/admin/organizations?*", async route =>
		{
			if (route.Request.Method == "GET")
				await Task.Delay(1500);
			await route.ContinueAsync();
		});

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/administration");

		// The Organizations section renders before the Users section, so its
		// Spinner (the one actually delayed above) is the first [role='status'].
		var loadingStatus = Page.Locator("[role='status']").First;
		await Expect(loadingStatus).ToBeVisibleAsync();
		await Expect(loadingStatus).ToContainTextAsync("Loading");
		await Expect(loadingStatus.Locator("svg.animate-spin")).ToBeVisibleAsync();

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(loadingStatus).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	private async Task<string> CreateIndividualContactOpportunityAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var suffix = Guid.NewGuid().ToString("N");

		using var tokenHttp = new HttpClient { BaseAddress = keycloak };
		var tokenResponse = await tokenHttp.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = "olaf",
				["password"] = "olaf123",
				["scope"] = "openid",
			}));
		tokenResponse.EnsureSuccessStatusCode();
		var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
		var token = tokenBody.GetProperty("access_token").GetString();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var orgsResponse = await http.GetAsync("/v1/organizations");
		orgsResponse.EnsureSuccessStatusCode();
		var orgs = await orgsResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = orgs.EnumerateArray().First().GetProperty("id").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"LoadingState {label} {suffix}",
			description = "Created by LoadingStateTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opp = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return opp.GetProperty("id").GetString()
			?? throw new InvalidOperationException("Created opportunity had no id.");
	}
}
