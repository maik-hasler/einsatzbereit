using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Coverage for #1041: check-in had no way back - IsCheckedIn had exactly one
/// writer (CheckIn()) and no method cleared it short of Reactivate(), which
/// requires the engagement to already be terminated. An organizer mis-click
/// or wrong QR scan permanently locked a volunteer into "attended". These
/// tests cover the new Engagement.UndoCheckIn() guard rails plus the
/// organizer-facing "Undo check-in" action on EngagementManagementPage.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementUndoCheckInTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task UndoCheckInEngagement_ClearsCheckedInFlag_WhenCalledByOrganizer()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(olafHttp, "UndoCheckInHappyPath");
		var engagementId = await CreateAndConfirmEngagementAsync(veraHttp, olafHttp, opportunityId);

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null))
			.EnsureSuccessStatusCode();

		var undoResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/undo-check-in", null);
		undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var undoBody = await undoResponse.Content.ReadFromJsonAsync<JsonElement>();
		undoBody.GetProperty("status").GetString().Should().Be(
			"Confirmed",
			"undoing a check-in must not change the engagement's Confirmed status, only the check-in flag");

		var engagement = await GetEngagementAsync(olafHttp, opportunityId, engagementId);
		engagement.GetProperty("isCheckedIn").GetBoolean().Should().BeFalse();
	}

	[Test]
	public async Task UndoCheckInEngagement_Returns409_WhenEngagementIsNotCheckedIn()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(olafHttp, "UndoCheckInNotCheckedIn");
		var engagementId = await CreateAndConfirmEngagementAsync(veraHttp, olafHttp, opportunityId);

		var undoResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/undo-check-in", null);

		undoResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
		var problem = await undoResponse.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("errorCode").GetString().Should().Be("Engagement.CheckInNotActive");
	}

	[Test]
	public async Task UndoCheckInEngagement_Returns409_WhenEngagementIsTerminated()
	{
		// A checked-in engagement that gets cancelled afterwards keeps
		// IsCheckedIn = true (Cancel() never touches it - the exact
		// unlabelled state the issue calls out) - undo-check-in must still
		// refuse to reopen a terminated engagement.
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(olafHttp, "UndoCheckInTerminated");
		var engagementId = await CreateAndConfirmEngagementAsync(veraHttp, olafHttp, opportunityId);

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null))
			.EnsureSuccessStatusCode();
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/cancel", null))
			.EnsureSuccessStatusCode();

		var undoResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/undo-check-in", null);

		undoResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
		var problem = await undoResponse.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("errorCode").GetString().Should().Be("Engagement.AlreadyTerminated");
	}

	[Test]
	public async Task UndoCheckInEngagement_Returns403_WhenCallerIsNotOrganizer()
	{
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");
		using var adminHttp = new HttpClient { BaseAddress = backend };
		adminHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "admin", "admin123")}");

		var (opportunityId, _) = await CreateIndividualContactOpportunityAsync(olafHttp, "UndoCheckInForbidden");
		var engagementId = await CreateAndConfirmEngagementAsync(veraHttp, olafHttp, opportunityId);

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null))
			.EnsureSuccessStatusCode();

		// admin is not a member of olaf's organization, so this must be
		// rejected before ever touching the check-in flag.
		var undoResponse = await adminHttp.PostAsync($"/v1/engagements/{engagementId}/undo-check-in", null);

		undoResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

		var engagement = await GetEngagementAsync(olafHttp, opportunityId, engagementId);
		engagement.GetProperty("isCheckedIn").GetBoolean().Should().BeTrue();
	}

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

	private static async Task<JsonElement> GetEngagementAsync(HttpClient olafHttp, string opportunityId, string engagementId)
	{
		var response = await olafHttp.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/engagements?pageNumber=1&pageSize=10");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		foreach (var item in body.GetProperty("items").EnumerateArray())
		{
			if (item.GetProperty("id").GetString() == engagementId)
				return item;
		}

		throw new InvalidOperationException($"Engagement '{engagementId}' not found in GetEngagements response.");
	}
}
