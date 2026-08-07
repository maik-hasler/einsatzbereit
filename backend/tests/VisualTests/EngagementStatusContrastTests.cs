using System.Net.Http.Json;
using System.Text.Json;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #659: the "Signed up: {date}" line (text-gray-400 on white)
/// and the Withdrawn status badge (text-gray-500 on bg-gray-100) failed WCAG
/// AA color-contrast. MyEngagementsPage_AsVera_HasNoSeriousA11yViolations in
/// AccessibilityTests.cs only caught this when a dated/Withdrawn engagement
/// happened to already be present for vera at scan time - seed/timing
/// dependent under the shared AspireFixture session. This test deterministically
/// creates a Withdrawn engagement first, so the violation is always exercised.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementStatusContrastTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagementsPage_WithWithdrawnEngagement_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend);

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");

		var applyResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "EngagementStatusContrastTests application." });
		applyResponse.EnsureSuccessStatusCode();
		var applied = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = applied.GetProperty("id").GetString()!;

		var withdrawResponse = await http.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #675 split the tab into "Current & Upcoming" (default) and "Past" -
		// a Withdrawn engagement now only shows up under "Past".
		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		// Confirm the Withdrawn badge this fix targets is actually on the page
		// before scanning - otherwise a pass proves nothing.
		await Expect(Page.GetByText("Withdrawn").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical")
			.ToList();

		if (violations.Count > 0)
		{
			var summary = string.Join("\n", violations.Select(v =>
				$"[{v.Impact}] {v.Id}: {v.Description}\n" +
				string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));
			throw new Exception($"Axe found {violations.Count} serious/critical a11y violation(s):\n{summary}");
		}
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

	private static async Task<string> CreateIndividualContactOpportunityAsync(Uri keycloak, Uri backend)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs (see EngagementReactivationTests).
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"EngagementStatusContrast Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"EngagementStatusContrast {suffix}",
			description = "Created by EngagementStatusContrastTests",
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
