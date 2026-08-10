using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #667: GetByVolunteerAsync (backing GET /v1/me/engagements,
/// the volunteer's "My Profile -> Engagements" list) used an inner join
/// against VolunteerOpportunitiesQuery. Deleting an opportunity hard-deletes
/// that row while only cancelling (not deleting) affected Engagement rows,
/// so the inner join silently dropped the volunteer's own engagement entirely
/// once its opportunity was gone - it should still appear, marked Cancelled,
/// with a fallback title.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementHistoryForDeletedOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagements_StillListsEngagement_AfterItsOpportunityIsDeleted()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "EngHistDeleted");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		var engagementId = await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		confirmResponse.EnsureSuccessStatusCode();

		var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}");
		deleteResponse.EnsureSuccessStatusCode();

		var myEngagementsResponse = await veraHttp.GetAsync("/v1/me/engagements?pageNumber=1&pageSize=50&upcoming=false");
		myEngagementsResponse.EnsureSuccessStatusCode();
		var myEngagements = await myEngagementsResponse.Content.ReadFromJsonAsync<JsonElement>();

		var engagement = myEngagements.GetProperty("items").EnumerateArray()
			.FirstOrDefault(e => e.GetProperty("id").GetString() == engagementId);

		engagement.ValueKind.Should().NotBe(JsonValueKind.Undefined,
			"the engagement must still appear in the volunteer's own history, not disappear entirely, once its opportunity is deleted");
		engagement.GetProperty("status").GetString().Should().Be("Cancelled");
		engagement.GetProperty("opportunityTitle").ValueKind.Should().Be(JsonValueKind.Null,
			"the opportunity backing this engagement no longer exists, so its title can no longer be resolved");
	}

	[Test]
	public async Task MyEngagementsPage_ShowsFallbackTitle_ForDeletedOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "EngHistDeletedUi");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var deleteResponse = await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}");
		deleteResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #675 split the tab into "Current & Upcoming" (default) and "Past" -
		// the opportunity's deletion cancels the engagement, so it now only
		// shows up under "Past".
		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		await Expect(Page.GetByText("This opportunity has been removed").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	/// <summary>
	/// Regression for #703: a Pending/Confirmed-but-not-checked-in engagement
	/// whose opportunity was removed without going through
	/// DeleteVolunteerOpportunityCommandHandler's cancellation step (e.g. data
	/// predating that safeguard) has no date field left to compare against and
	/// no code path to re-evaluate it, so it stayed in "Current & Upcoming"
	/// forever. The row is deleted directly here (bypassing the DELETE
	/// endpoint, which already cancels active engagements on the normal path)
	/// to reproduce that stale state.
	/// </summary>
	[Test]
	public async Task MyEngagementsPage_MovesToPast_ForNonTerminalEngagementWithGoneOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "EngHistOrphanedUi");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		await ApplyAsync(veraHttp, opportunityId, "Please let me help.");

		await Fixture.DeleteOpportunityRowDirectlyAsync(Guid.Parse(opportunityId));

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Default tab is "Current & Upcoming" - the orphaned Pending engagement
		// must not appear here.
		await Expect(Page.GetByText("This opportunity has been removed"))
			.Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		await Expect(Page.GetByText("This opportunity has been removed").First)
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
			new { name = $"EngHistDeletedOpp Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain Organization
		// aggregate (unlike GetOrganizations, which projects to a DTO), so
		// its strongly-typed OrganizationId record struct serializes as a
		// nested { "value": "<guid>" } object rather than a plain string.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"EngHistDeletedOpp {label} {suffix}",
			description = "Created by EngagementHistoryForDeletedOpportunityTests",
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
