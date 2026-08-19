using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1051: the backend fully supported an engagement cancellation
/// reason (domain model, command, email, endpoint) but EngagementManagementPage
/// always called cancelEngagement with a null body, and EngagementSummary had no
/// CancellationReason field for the volunteer's own activity list to render. Now
/// the organizer's cancel dialog collects an optional reason and the volunteer's
/// "My profile -> Engagements" list shows it on the Cancelled row.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementCancellationReasonTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_CancelDialog_SendsEnteredReason_ToBackend()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "CancelReasonOrganizer");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		IResponse? cancelResponse = null;
		Page.Response += (_, response) =>
		{
			if (response.Url.Contains("/cancel", StringComparison.Ordinal))
				cancelResponse = response;
		};

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		await Page.Locator("#cancel-reason").FillAsync("Position no longer available");
		await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, cancel" }).ClickAsync();

		await PollUntilAsync(
			() => Task.FromResult(cancelResponse is not null),
			() => "Expected the cancel request to reach the backend.");

		var body = await cancelResponse!.JsonAsync();
		body!.Value.GetProperty("cancellationReason").GetString().Should().Be("Position no longer available");
	}

	[Test]
	public async Task ProfileActivitySection_ShowsCancellationReason_ForCancelledEngagement()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(keycloak, backend, "CancelReasonVolunteer");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var engagementsResponse = await olafHttp.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/engagements?pageNumber=1&pageSize=10");
		engagementsResponse.EnsureSuccessStatusCode();
		var engagements = await engagementsResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagements.GetProperty("items").EnumerateArray().First().GetProperty("id").GetString();

		var cancelResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/engagements/{engagementId}/cancel",
			new { reason = "Volunteer no longer needed" });
		cancelResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		await Expect(Page.GetByText("Reason: Volunteer no longer needed"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
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

	private static async Task<(string OpportunityId, string OrganizationId)> CreateIndividualContactOpportunityAsync(
		Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs, which made GET
		// /v1/organizations intermittently race to an empty list here.
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by EngagementCancellationReasonTests",
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
}
