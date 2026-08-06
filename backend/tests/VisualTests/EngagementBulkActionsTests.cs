using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Coverage for #1682: c3f9ada1 shipped bulk confirm/cancel (#1044) - selection
/// state, partial-failure handling, the bulk action bars - with no test at any
/// level. This drives a real bulk-confirm through the browser where one of the
/// two selected engagements has already been confirmed out of band by the time
/// the click lands (a stale checkbox against a since-changed server state), and
/// asserts both the partial-failure toast and that only the row the backend
/// actually confirmed flips to Confirmed in the UI - EngagementManagementPage.tsx's
/// handleBulkConfirm only patches local state for ids in the response's
/// succeeded list, leaving a failed id both visibly Pending and still selected.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class EngagementBulkActionsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementManagementPage_BulkConfirm_ReportsPartialFailure_WhenOneEngagementWasAlreadyConfirmed()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olafToken = await GetTokenAsync(keycloak, "olaf", "olaf123");
		var (opportunityId, organizationId, firstSlotId, secondSlotId) =
			await CreateScheduledSlotsOpportunityWithTwoSlotsAsync(keycloak, backend, olafToken, "BulkConfirmPartial");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		var firstEngagementId = await ApplyForSlotAsync(veraHttp, opportunityId, firstSlotId);
		var secondEngagementId = await ApplyForSlotAsync(veraHttp, opportunityId, secondSlotId);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.Locator("#select-all-pending")).ToBeVisibleAsync();

		// Simulate a concurrent action: by the time the bulk-confirm click below
		// lands, the second engagement has already been confirmed out of band
		// (e.g. from a second organizer's tab) - the page's own engagement list
		// was fetched before this and still shows it as Pending and selectable.
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");
		var preemptResponse = await olafHttp.PostAsync($"/v1/engagements/{secondEngagementId}/confirm", content: null);
		preemptResponse.EnsureSuccessStatusCode();

		await Page.Locator("#select-all-pending").CheckAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm selected" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Alert).GetByText("1 confirmed, 1 failed."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The engagement the backend actually confirmed flips to Confirmed (and
		// gains its Revoke control) in the UI...
		await Expect(Page.Locator($"[data-testid='engagement-revoke-{firstEngagementId}']"))
			.ToBeVisibleAsync();

		// ...the one that failed server-side (Engagement.NotPending) never does.
		await Expect(Page.Locator($"[data-testid='engagement-revoke-{secondEngagementId}']"))
			.Not.ToBeVisibleAsync();

		// Only succeeded ids are dropped from the selection - the failed row
		// stays checked, the per-item signal a partial failure leaves behind.
		await Expect(Page.Locator("li input[type='checkbox']:checked")).ToHaveCountAsync(1);
	}

	private static async Task<string> ApplyForSlotAsync(HttpClient http, string opportunityId, string timeSlotId)
	{
		var response = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { timeSlotId });
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

	private static async Task<(string OpportunityId, string OrganizationId, string FirstSlotId, string SecondSlotId)>
		CreateScheduledSlotsOpportunityWithTwoSlotsAsync(Uri keycloak, Uri backend, string olafToken, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		// Fresh organization rather than olaf's shared seed org - see the
		// identical note in EngagementManagementFiltersTests.
		var createOrgResponse = await http.PostAsJsonAsync(
			"/v1/organizations",
			new { name = $"{label} Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		// Created as a draft: a ScheduledSlots opportunity can't be published
		// until it has at least one time slot. ValidUntil is omitted - it is
		// only allowed for IndividualContact opportunities (VolunteerOpportunity.
		// Create rejects a non-null ValidUntil for any other participation type).
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"{label} {suffix}",
			description = "Created by EngagementBulkActionsTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()!;

		// One recurring call creates both slots at once.
		var slotsResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new
			{
				startDateTime = DateTimeOffset.UtcNow.AddDays(7),
				endDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				maxParticipants = 10,
				recurrenceFrequency = "Weekly",
				recurrenceCount = 2,
			});
		slotsResponse.EnsureSuccessStatusCode();
		var slots = (await slotsResponse.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
		var firstSlotId = slots[0].GetProperty("id").GetString()!;
		var secondSlotId = slots[1].GetProperty("id").GetString()!;

		var publishResponse = await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null);
		publishResponse.EnsureSuccessStatusCode();

		return (opportunityId, organizationId, firstSlotId, secondSlotId);
	}
}
