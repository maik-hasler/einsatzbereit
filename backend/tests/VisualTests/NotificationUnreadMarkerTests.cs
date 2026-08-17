using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1786: an unread notification row was marked only by an
/// empty decorative dot span plus a font-weight/colour difference, so read and
/// unread rows were indistinguishable to a screen reader and unread state was
/// announced only in aggregate on the bell (WCAG 2.2 A, 1.4.1 Use of Color).
/// axe cannot catch this - an unlabelled decorative span violates no rule,
/// which is why AccessibilityTests' NotificationDropdown_Open scan stayed green
/// throughout - so the per-row marker needs its own targeted assertion.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationUnreadMarkerTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Anchored at the start so it matches the row's own select button (whose
	// accessible name now begins with the hidden marker) and never the sibling
	// mark-unread action button, whose aria-label is "Mark as unread: {text}".
	// The suite runs in English - nothing pins the locale, so Playwright's
	// default en-US browser locale feeds i18next's navigator detector (see
	// NavigationTests' html[lang="en"] assertion on a bare page load).
	private static readonly Regex UnreadMarkerName = new("^Unread\\b");

	[Test]
	public async Task UnreadNotificationRow_ExposesHiddenUnreadMarker_ReadRowDoesNot()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		// A fresh org rather than olaf's shared seed org - other VisualTests in
		// this shared Aspire session mutate/delete shared orgs concurrently.
		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"NotifUnreadMarker Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		// CreateOrganizationEndpoint returns the raw domain aggregate, so its
		// strongly-typed OrganizationId serializes as { "value": "<guid>" }.
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		// Two opportunities, so one notification can be flipped to read and the
		// other left unread - both rows are then asserted against the same open
		// panel rather than across two separate page states.
		var readTitle = $"NotifUnreadMarker Read {suffix}";
		var unreadTitle = $"NotifUnreadMarker Unread {suffix}";
		var readOpportunityId = await CreateOpportunityAsync(olafHttp, organizationId, readTitle);
		var unreadOpportunityId = await CreateOpportunityAsync(olafHttp, organizationId, unreadTitle);

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		// CreateEngagementCommandHandler writes the organizer's notification row
		// inside the request's own transaction (only the emails go through the
		// outbox), so both rows exist as soon as these POSTs return.
		await ApplyAsync(veraHttp, readOpportunityId);
		await ApplyAsync(veraHttp, unreadOpportunityId);

		var readNotificationId = await GetNotificationIdByTitleAsync(olafHttp, readTitle);
		var markReadResponse = await olafHttp.PostAsync($"/v1/notifications/{readNotificationId}/read", null);
		markReadResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// The list is fetched when the dropdown opens (only the unread count is
		// polled), so the rows need the same generous wait as NotificationTests.
		var unreadRow = panel.Locator("li", new() { HasText = unreadTitle }).First;
		await Expect(unreadRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var readRow = panel.Locator("li", new() { HasText = readTitle }).First;
		await Expect(readRow).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(unreadRow.GetByRole(AriaRole.Button, new() { NameRegex = UnreadMarkerName }))
			.ToHaveCountAsync(1);
		await Expect(readRow.GetByRole(AriaRole.Button, new() { NameRegex = UnreadMarkerName }))
			.ToHaveCountAsync(0);

		// The marker must stay invisible on screen - the coloured dot remains the
		// visual shorthand. Not a Not.ToBeVisibleAsync() check: Tailwind's sr-only
		// is clip-based rather than display:none (deliberately, so the node stays
		// in the accessibility tree), which Playwright still counts as visible -
		// same reasoning as LiveRegionTests' SuccessBanner assertion.
		await Expect(unreadRow.Locator("span.sr-only")).ToHaveTextAsync("Unread");
		await Expect(readRow.Locator("span.sr-only")).ToHaveCountAsync(0);
	}

	private static async Task<string> CreateOpportunityAsync(HttpClient http, string organizationId, string title)
	{
		var response = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by NotificationUnreadMarkerTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		response.EnsureSuccessStatusCode();
		var opportunity = await response.Content.ReadFromJsonAsync<JsonElement>();
		return opportunity.GetProperty("id").GetString()!;
	}

	private static async Task ApplyAsync(HttpClient http, string opportunityId)
	{
		var response = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Notify Olaf please." });
		response.EnsureSuccessStatusCode();
	}

	private static async Task<string?> GetNotificationIdByTitleAsync(HttpClient http, string relatedTitle)
	{
		var response = await http.GetAsync("/v1/notifications");
		response.EnsureSuccessStatusCode();
		var page = await response.Content.ReadFromJsonAsync<JsonElement>();
		// GetMyNotifications returns a NotificationsPage ({ items, hasMore }),
		// not a bare array, since einsatzbereit#1384 added cursor pagination.
		// Matching on the unique per-run title keeps this from picking up
		// another test's EngagementCreated row in the shared session.
		return page.GetProperty("items").EnumerateArray()
			.First(n => n.GetProperty("relatedTitle").GetString() == relatedTitle)
			.GetProperty("id").GetString();
	}
}
