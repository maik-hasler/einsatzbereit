using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2064: the QR check-in modal used to show the raw,
/// unlabeled 36-character engagement UUID as the manual-scan fallback. It
/// now shows a labeled, 8-character human-transferable prefix instead.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInModalQrFallbackCodeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CheckInModal_ShowsLabeledShortFallbackCode_ForQrCheckInMethod()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = $"CheckInQrFallback Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckInQrFallback Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by CheckInModalQrFallbackCodeTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
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
			new { message = "Applying via CheckInModalQrFallbackCodeTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// EngagementReadRepository.GetByVolunteerAsync orders "Current &
		// upcoming" by time-slot start, and this opportunity has none - so on a
		// shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, this row can
		// land past the first (10-item) page instead of being visible
		// immediately, so page through to it - same pattern as
		// CheckInModalDeletedOpportunityTests.
		var row = Page.Locator("li", new() { HasText = oppTitle });
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(row);
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var checkInButton = row.GetByRole(AriaRole.Button, new() { Name = "Check in" });
		await Expect(checkInButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await checkInButton.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await Expect(dialog.GetByText("If the scan doesn't work, tell the organizer this code:"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var fallbackCode = Page.GetByTestId("checkin-fallback-code");
		await Expect(fallbackCode).ToBeVisibleAsync();
		await Expect(fallbackCode).ToHaveTextAsync(engagementId[..8]);

		// The dialog must show the short prefix only - not the full 36-char
		// UUID this issue was filed about.
		await Expect(dialog.GetByText(engagementId, new() { Exact = true })).Not.ToBeVisibleAsync();
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
