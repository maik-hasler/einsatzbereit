using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Coverage for #1046: the organizer's "Manage sign-ups" page gained a
/// status filter (mirroring the volunteer-side scope toggle in
/// ActivitySection) on top of the pagination added by #1456. This asserts
/// the status dropdown actually narrows the visible rows instead of just
/// being wired up server-side.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementManagementFiltersTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ManageApplicationsPage_FiltersByStatus_WhenOrganizerSelectsAStatus()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "EngagementFilters");
		await CreateAndConfirmVeraEngagementAsync(keycloak, backend, opportunityId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Expect(Page.GetByText("Vera Volunteer")).ToBeVisibleAsync();

		var statusFilter = Page.Locator("#engagement-status-filter");
		await statusFilter.SelectOptionAsync(new SelectOptionValue { Label = "Pending" });

		await Expect(Page.GetByText("No sign-ups match your filters.")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Vera Volunteer")).Not.ToBeVisibleAsync();

		await statusFilter.SelectOptionAsync(new SelectOptionValue { Label = "Confirmed" });

		await Expect(Page.GetByText("Vera Volunteer")).ToBeVisibleAsync();
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
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by EngagementManagementFiltersTests",
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
			new { message = "Filter test signup" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		confirmResponse.EnsureSuccessStatusCode();
	}
}
