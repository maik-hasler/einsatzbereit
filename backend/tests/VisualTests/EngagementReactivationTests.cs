using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #648: re-applying to an opportunity after withdrawing kept
/// the original application's CreatedOn timestamp, because
/// CreateEngagementCommandHandler reuses the existing terminal Engagement row
/// via Engagement.Reactivate(...) instead of inserting a new one, and
/// AuditableEntityInterceptor only stamps CreatedOn on EntityState.Added -
/// never on the Modified state a reactivation produces. Both the volunteer's
/// "My Profile -> Engagements" tab and the organizer's "Manage applications"
/// page kept showing the stale original date.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementReactivationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Reactivate_RefreshesCreatedOn_AfterWithdrawThenReapply()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "Reactivation");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");

		var firstEngagement = await ApplyAsync(http, opportunityId, "Original application.");
		var firstCreatedOn = await GetCreatedOnAsync(http, opportunityId);

		var withdrawResponse = await http.PostAsync($"/v1/engagements/{firstEngagement}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		// Ensure the reactivation happens measurably later than the original
		// application, so a frozen CreatedOn (the bug) is trivially
		// distinguishable from a refreshed one.
		await Task.Delay(2000);

		var secondEngagement = await ApplyAsync(http, opportunityId, "Re-application after withdrawal.");
		secondEngagement.Should().Be(firstEngagement, "reactivation reuses the same terminal engagement row");

		var secondCreatedOn = await GetCreatedOnAsync(http, opportunityId);

		secondCreatedOn.Should().BeAfter(firstCreatedOn,
			"Engagement.Reactivate must refresh CreatedOn to the re-application time, not leave it frozen at the original application's date");

		// Leave vera's account clean for the rest of this shared Aspire session.
		var cleanupResponse = await http.PostAsync($"/v1/engagements/{secondEngagement}/withdraw", content: null);
		cleanupResponse.EnsureSuccessStatusCode();
	}

	[Test]
	public async Task EngagementsTab_RendersReactivatedEngagement()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ReactivationUi");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");

		var engagementId = await ApplyAsync(http, opportunityId, "Original application.");
		var withdrawResponse = await http.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();
		var reactivatedEngagementId = await ApplyAsync(http, opportunityId, "Re-application after withdrawal.");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Withdraw" }).First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Leave vera's account clean for the rest of this shared Aspire session.
		var cleanupResponse = await http.PostAsync($"/v1/engagements/{reactivatedEngagementId}/withdraw", content: null);
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

	private static async Task<DateTimeOffset> GetCreatedOnAsync(HttpClient http, string opportunityId)
	{
		var response = await http.GetAsync("/v1/me/engagements");
		response.EnsureSuccessStatusCode();
		var engagements = await response.Content.ReadFromJsonAsync<JsonElement>();
		var match = engagements.EnumerateArray()
			.First(e => e.GetProperty("opportunityId").GetString() == opportunityId);
		return match.GetProperty("createdOn").GetDateTimeOffset();
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
			new { name = $"EngagementReactivation Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"EngagementReactivation {label} {suffix}",
			description = "Created by EngagementReactivationTests",
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
