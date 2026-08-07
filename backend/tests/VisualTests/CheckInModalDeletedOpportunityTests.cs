using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #686: CheckInModal's opportunity-details fetch had no
/// .catch(), so a 404 (opportunity deleted after the engagements list was
/// loaded, e.g. by an organizer in another tab, but before the volunteer
/// clicks the still-rendered "Check in" button) left the modal stuck on
/// "Loading..." forever with an unhandled promise rejection.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInModalDeletedOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CheckInModal_ShowsFriendlyError_WhenOpportunityDeletedAfterListLoaded()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInModalDeleted Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInModalDeleted Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CheckInModalDeletedOpportunityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			// einsatzbereit#1016: the "Check in" button only renders for QRCode/PINCode
			// opportunities now, so this race (organizer deletes the opportunity between
			// list load and button click) needs a method that still shows the button.
			checkInMethod = "QRCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Applying via CheckInModalDeletedOpportunityTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = oppTitle });

		// EngagementReadRepository.GetByVolunteerAsync orders "Current &
		// Upcoming" by time-slot start, and this opportunity has none - so on a
		// shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, this row can
		// land past the first (10-item) page instead of being visible
		// immediately, so page through to it.
		//
		// Wait for the first page before starting: the WaitForLoadStateAsync
		// above can settle before the engagements fetch is even issued, since
		// useLoadMore only requests from an effect after React commits.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(row);

		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var checkInButton = row.GetByRole(AriaRole.Button, new() { Name = "Check in" });
		await Expect(checkInButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Simulate the race: the organizer deletes the opportunity in another
		// tab/session after the volunteer's list has already loaded, but before
		// they click the still-rendered "Check in" button.
		(await olafHttp.DeleteAsync($"/v1/volunteer-opportunities/{opportunityId}"))
			.EnsureSuccessStatusCode();

		await checkInButton.ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await Expect(dialog.GetByText("This opportunity is no longer available."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(dialog.GetByText("Loading…")).Not.ToBeVisibleAsync();
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
}
