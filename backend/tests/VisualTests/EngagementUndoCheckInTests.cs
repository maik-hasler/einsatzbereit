using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The organizer-facing half of #1041: undoing a check-in on
/// EngagementManagementPage must swap the "Checked in" badge back for the
/// "Mark as checked in" button, so a mis-click is visibly recoverable.
///
/// The four status-code and IsCheckedIn-flag assertions that used to sit
/// beside it moved to <c>IntegrationTests/EngagementUndoCheckInTests.cs</c>
/// in einsatzbereit#2148 - they opened no page at all, and paid for a
/// Chromium context and a frontend to make plain HTTP calls.
///
/// Background: check-in had no way back. IsCheckedIn had exactly one writer
/// (CheckIn()) and nothing cleared it short of Reactivate(), which requires
/// the engagement to already be terminated - so an organizer mis-click or a
/// wrong QR scan locked a volunteer into "attended" permanently.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementUndoCheckInTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ManageApplicationsPage_UndoCheckIn_RestoresManualCheckInButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var (opportunityId, organizationId) = await CreateIndividualContactOpportunityAsync(olafHttp, "UndoCheckInUi", checkInMethod: "Manual");
		await CreateAndConfirmEngagementAsync(veraHttp, olafHttp, opportunityId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.Locator("li", new() { HasText = "Vera Volunteer" });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await row.GetByRole(AriaRole.Button, new() { Name = "Mark as checked in" }).ClickAsync();
		// Exact: true - GetByText's default substring/case-insensitive match
		// would otherwise also match the "Mark as checked in" button's label
		// (which contains "checked in"), making the post-undo assertion below
		// find that still-visible button instead of the (by-then-gone) badge.
		var checkedInBadge = row.GetByText("Checked in", new() { Exact = true });
		await Expect(checkedInBadge).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var undoButton = row.GetByRole(AriaRole.Button, new() { Name = "Undo check-in" });
		await Expect(undoButton).ToBeVisibleAsync();

		await undoButton.ClickAsync();

		await Expect(checkedInBadge).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Mark as checked in" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	private static async Task<(string OpportunityId, string OrganizationId)> CreateIndividualContactOpportunityAsync(
		HttpClient olafHttp, string label, string checkInMethod = "None")
	{
		var suffix = Guid.NewGuid().ToString("N");

		var createOrgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by EngagementUndoCheckInTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod,
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return (opportunity.GetProperty("id").GetString()!, organizationId);
	}

	private static async Task<string> CreateAndConfirmEngagementAsync(HttpClient veraHttp, HttpClient olafHttp, string opportunityId)
	{
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Undo check-in test signup" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null);
		confirmResponse.EnsureSuccessStatusCode();

		return engagementId;
	}
}
