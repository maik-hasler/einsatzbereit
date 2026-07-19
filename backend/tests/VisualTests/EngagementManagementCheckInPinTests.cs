using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #665: EngagementManagementPage ("Manage applications")
/// unconditionally called GET .../check-in-pin on every load, regardless of
/// the opportunity's checkInMethod. The backend returns 404 whenever the PIN
/// is null, i.e. for every checkInMethod other than "PINCode" - so the
/// request was a guaranteed, silently-swallowed 404 for 3 of the 4 possible
/// values. The fix gates the fetch on checkInMethod === "PINCode", the same
/// condition already used to render the PIN block.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementManagementCheckInPinTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ManageApplicationsPage_DoesNotRequestCheckInPin_WhenCheckInMethodIsNone()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "CheckInPinNone", "None");

		var pinRequested = false;
		await Page.RouteAsync("**/*check-in-pin*", async route =>
		{
			pinRequested = true;
			await route.ContinueAsync();
		});

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		pinRequested.Should().BeFalse(
			"checkInMethod is \"None\", so the PIN endpoint would always 404 and must not be called");
	}

	[Test]
	public async Task ManageApplicationsPage_RequestsCheckInPin_WhenCheckInMethodIsPINCode()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "CheckInPinPin", "PINCode");

		var pinResponseStatuses = new List<int>();
		Page.Response += (_, response) =>
		{
			if (response.Url.Contains("check-in-pin", StringComparison.Ordinal))
				pinResponseStatuses.Add(response.Status);
		};

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		pinResponseStatuses.Should().ContainSingle().Which.Should().Be(200);
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
		Uri keycloak, Uri backend, string label, string checkInMethod)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs, which made GET
		// /v1/organizations intermittently race to an empty list here.
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"CheckInPin Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"CheckInPin {label} {suffix}",
			description = "Created by EngagementManagementCheckInPinTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod,
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return (opportunity.GetProperty("id").GetString()!, organizationId);
	}
}
