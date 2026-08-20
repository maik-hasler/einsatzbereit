using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #675: "My profile -> Engagements" split into "Current &amp;
/// upcoming" (default) and "Past" tabs, each paginated, instead of one
/// unbounded flat list. A Pending engagement should only show up under
/// "Current &amp; upcoming", and a Withdrawn one only under "Past".
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeTabsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementsTab_SplitsPendingAndWithdrawn_AcrossUpcomingAndPastScopes()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var upcomingOpportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsUpcoming");
		var pastOpportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsPast");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var upcomingEngagementId = await ApplyAsync(veraHttp, upcomingOpportunityId, "Still pending.");
		var pastEngagementId = await ApplyAsync(veraHttp, pastOpportunityId, "About to withdraw.");

		var withdrawResponse = await veraHttp.PostAsync($"/v1/engagements/{pastEngagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile?tab=engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Default "Current & upcoming" scope: the still-pending engagement is
		// visible, the withdrawn one is not.
		//
		// The pending engagement has no time slot (IndividualContact), and
		// EngagementReadRepository.GetByVolunteerAsync orders the "Current &
		// upcoming" scope by time-slot start (entries with none sort last) - so on
		// a shared session where other concurrently-running tests have already
		// given vera their own time-slotted upcoming engagements, it can land past
		// the first (10-item) page, so page through to it. Wait for the first page
		// before starting - see LoadMoreUntilVisibleAsync for why the hand-rolled
		// walk this replaces exited after a single click.
		var upcomingText = Page.GetByText("ScopeTabsUpcoming").First;
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(upcomingText);

		await Expect(upcomingText).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("ScopeTabsPast")).Not.ToBeVisibleAsync();

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();
		await Expect(Page.GetByText("ScopeTabsPast").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("ScopeTabsUpcoming")).Not.ToBeVisibleAsync();

		// Leave vera's account clean for the rest of this shared Aspire session.
		var cleanupResponse = await veraHttp.PostAsync($"/v1/engagements/{upcomingEngagementId}/withdraw", content: null);
		cleanupResponse.EnsureSuccessStatusCode();
	}

	/// <summary>
	/// Regression for #2070: a Withdrawn engagement whose opportunity still has
	/// a future "express interest by" deadline (the common shape for an
	/// IndividualContact opportunity - it stays open for other volunteers long
	/// after this one withdrew) used to keep showing that future-dated deadline
	/// on its card in the "Past" scope, contradicting the scope's own label.
	/// Once terminal (Cancelled/Withdrawn), the deadline is no longer
	/// actionable for this engagement, so the card should drop it and rely on
	/// the status chip instead.
	/// </summary>
	[Test]
	public async Task EngagementsTab_PastScope_HidesFutureApplyByDeadline_ForWithdrawnEngagement()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateIndividualContactOpportunityAsync(keycloak, backend, "ScopeTabsWithdrawnFuture");

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var engagementId = await ApplyAsync(veraHttp, opportunityId, "Withdrawing right away.");
		var withdrawResponse = await veraHttp.PostAsync($"/v1/engagements/{engagementId}/withdraw", content: null);
		withdrawResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Exact match: the opportunity's own fixture title ("ScopeTabsWithdrawnFuture")
		// contains "Withdrawn" as a substring too, so a non-exact GetByText here
		// is a strict-mode violation - it resolves to both the title link and
		// the actual "Withdrawn" status badge this assertion means to check.
		await Expect(card.GetByText("Withdrawn", new() { Exact = true })).ToBeVisibleAsync();
		await Expect(card.GetByText("Express interest by")).Not.ToBeVisibleAsync();
	}

	/// <summary>
	/// Regression for #1855: Engagement.CheckIn() has no time-based guard, so an
	/// organizer can check a volunteer in as soon as an engagement is Confirmed -
	/// e.g. at arrival for a still-ongoing multi-hour shift, or (as filed) for a
	/// slot dated weeks out. EngagementReadRepository.GetByVolunteerAsync used to
	/// bucket any checked-in Confirmed engagement into "Past" unconditionally,
	/// with no comparison against the slot's own end time (#1163 already fixed
	/// the opposite-direction gap - an un-checked-in Confirmed engagement never
	/// leaving "Current &amp; upcoming" once its slot had ended). The volunteer's
	/// own "Past" tab then showed a "Checked in" chip and a "Leave feedback" CTA
	/// for a shift with a displayed date that had not happened yet.
	/// </summary>
	[Test]
	public async Task EngagementsTab_KeepsCheckedInEngagement_InUpcomingScope_WhileItsTimeSlotIsStillInTheFuture()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"CheckedInFuture Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"CheckedInFuture Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by MyEngagementsScopeTabsTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "Manual",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		// A multi-hour shift two weeks out - the same shape as the review finding
		// that filed #1855 (a slot dated well in the future).
		var start = DateTimeOffset.UtcNow.AddDays(14);
		var slotResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = start.AddHours(8), maxParticipants = 5, recurrenceCount = 1 });
		slotResponse.EnsureSuccessStatusCode();
		var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
		var timeSlotId = slots[0].GetProperty("id").GetString();

		(await olafHttp.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "ScheduledSlots", timeSlotId, message = (string?)null });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();

		// The organizer checks vera in well ahead of the slot's end - there is no
		// time-based guard on CheckIn() (see Domain/Engagements/Engagement.cs).
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");

		// Default "Current & upcoming" scope - the checked-in engagement belongs
		// here, not "Past", since its own displayed date has not happened yet.
		await Expect(Page.Locator("#activity [data-testid='engagement-card']").First)
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await LoadMoreUntilVisibleAsync(card);
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card).ToContainTextAsync(oppTitle);
		await Expect(card.GetByText("Checked in")).ToBeVisibleAsync();

		// "Past" must not present it as a completed, feedback-ready item (#1855).
		// Waits for the Past scope's own fetch to actually land (a card or its
		// empty state) before asserting absence - otherwise a still-loading list
		// would trivially satisfy Not.ToBeVisibleAsync without proving anything.
		var pastCardOrEmptyState = Page.Locator("#activity [data-testid='engagement-card']")
			.Or(Page.GetByText("No past sign-ups yet."));
		await Page.Locator("[data-testid='engagements-scope-past']").ClickAsync();
		await Expect(pastCardOrEmptyState.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card).Not.ToBeVisibleAsync();
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

	private static async Task<string> CreateIndividualContactOpportunityAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");

		// Create a fresh organization rather than reusing olaf's shared seed
		// org - other VisualTests running concurrently in this shared Aspire
		// session can mutate/delete shared orgs (see EngagementReactivationTests).
		var createOrgResponse = await PostJsonWithRetryAsync(http,
			"/v1/organizations",
			new { name = $"MyEngagementsScopeTabs Org {suffix}" });
		createOrgResponse.EnsureSuccessStatusCode();
		var org = await createOrgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"{label} {suffix}",
			descriptionDe = "Created by MyEngagementsScopeTabsTests",
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
