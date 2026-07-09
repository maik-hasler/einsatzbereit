using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #655: once a volunteer opportunity is deleted, the
/// EngagementCreated notification that was already generated for it resolves
/// its title via a live lookup of the (now-gone) opportunity, so
/// relatedTitle stays null forever, and its actionUrl 404s. Covers both the
/// frontend fallback title text and EngagementManagementPage rendering the
/// existing NotFoundPage instead of a raw error string on that 404.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationForDeletedOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Notification_KeepsNullRelatedTitle_AfterOpportunityDeleted()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "NotifDeletedTitle");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}");
		deleteResponse.EnsureSuccessStatusCode();

		var notification = await GetEngagementCreatedNotificationAsync(olafHttp, opportunityId);

		notification.TryGetProperty("relatedTitle", out var relatedTitle).Should().BeTrue();
		(relatedTitle.ValueKind is JsonValueKind.Null).Should().BeTrue(
			"the opportunity backing this notification no longer exists, so its title can no longer be resolved");
		notification.GetProperty("actionUrl").GetString().Should().Be(
			$"/volunteer-opportunities/{opportunityId}/engagements");
	}

	[Test]
	public async Task EngagementManagementPage_RendersNotFoundPage_ForDeletedOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "NotifDeletedUi");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}");
		deleteResponse.EnsureSuccessStatusCode();

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
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

	private static async Task<JsonElement> GetEngagementCreatedNotificationAsync(HttpClient http, string opportunityId)
	{
		var response = await http.GetAsync("/v1/me/notifications");
		response.EnsureSuccessStatusCode();
		var notifications = await response.Content.ReadFromJsonAsync<JsonElement>();
		return notifications.EnumerateArray()
			.First(n => n.GetProperty("kind").GetString() == "EngagementCreated"
				&& n.GetProperty("actionUrl").GetString() == $"/volunteer-opportunities/{opportunityId}/engagements");
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
		// session can mutate/delete shared orgs, which made GET
		// /v1/organizations intermittently race to an empty list here.
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"NotifDeletedOpp Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"NotifDeletedOpp {label} {suffix}",
			description = "Created by NotificationForDeletedOpportunityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return opportunity.GetProperty("id").GetString()!;
	}
}
