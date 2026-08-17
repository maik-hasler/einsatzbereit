using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1788 and #1848: copy defects on /profile, all in the
/// German locale that the app actually serves by default.
///
/// 1. The 100-confirmations badge was called "Hundertschaft" - in German first
///    a police/riot-unit term, a jarring association for a civic volunteering
///    product. Renamed to the plain, factual "100 Einsaetze".
/// 2. (#1788) "Auf Kurs" was described as a "Login-Serie" - Denglish, and it
///    hid the rule behind English loan vocabulary. Renamed "Aktive Woche" with
///    a German description that states the rule it actually measures (seven
///    days in a row).
/// 3. (#1848) "Aktive Woche" turned out to be its own regression: it reads as
///    a *weekly* activity concept, confusable with the very next badge on the
///    same grid, "weekly-hero-4" / "Wochenheld" - a genuinely different,
///    4-consecutive-*weeks* metric. Renamed again to "Anmeldeserie" (using the
///    app's own established "anmelden" login terminology, not the Denglish
///    "Login" noun #1788 removed) with a description that names the unit it
///    actually measures - days, not weeks. The award rule in
///    RecordLoginCommandHandler is unchanged throughout all of this, so the
///    description must not start implying volunteering activity the badge
///    does not track either.
/// 4. The activity-streak stat tile rendered a bare number over the unit-less
///    label "Aktivitaetsserie" - one what: day, week, shift? Unlike
///    engagementStatLabel it had no _one/_other forms, so it could not even
///    inflect. UserStreak.ActivityStreak counts consecutive ISO *weeks* with a
///    confirmed engagement, so the label now carries that unit.
/// 5. (#1935) With both stats correctly labelled per (4), the week-streak
///    tile ("X Wochen in Serie") and the day-streak caption ("Y Tage in
///    Folge angemeldet") still sat next to each other reading as
///    near-synonyms ("in Serie" vs. "in Folge") with different units and no
///    other cue to tell them apart. Each label now also names its own badge
///    ("Wochenheld" / "Anmeldeserie") so the two read as distinct at a
///    glance even when both are nonzero at once.
///
/// These are locale-file values, so nothing in the backend test suite would
/// otherwise notice them regressing.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AchievementCopyTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfileBadgeGrid_UsesCivicGermanCopy_NotHundertschaftOrLoginSerie()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// #1848: the page fires several authenticated requests concurrently on
		// mount (profile, streaks, achievements...), and LoginStreakMiddleware
		// only *awaits* RecordLoginCommand for whichever one wins the per-user
		// dedup race - the other concurrent requests see the cache entry
		// already set and skip straight to reading current DB state, which can
		// race ahead of the winner's still-in-flight write. Relying on the
		// page's own getMyStreaks() call to have deterministically recorded
		// the streak by the time it responds is therefore flaky. Seed it with
		// a single sequential HTTP call first instead, same reasoning as
		// SeedConfirmedEngagementForVeraAsync below.
		await SeedLoginStreakForVeraAsync(backend);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await SwitchToGermanAsync();

		// BadgeGrid renders every catalog entry, earned or not (only IsHidden
		// ones are masked, and neither of these is), so both cards are on the
		// page for any signed-in volunteer - no need to earn them first.
		await Expect(Page.Locator("#badge-name-centurion-100"))
			.ToHaveTextAsync("100 Einsätze", new() { Timeout = 20_000 });
		await Expect(Page.Locator("#badge-name-on-a-roll-7"))
			.ToHaveTextAsync("Anmeldeserie");

		// The description lives in the card for an unearned badge and in the
		// tooltip either way; the tooltip is the one that is always present.
		await Expect(Page.Locator("#badge-tooltip-on-a-roll-7"))
			.ToContainTextAsync("Verdient für 7 aufeinanderfolgende Tage mit Anmeldung.");

		// Nowhere in the DOM, not merely invisible - the tooltip copy counts too.
		await Expect(Page.GetByText("Hundertschaft")).ToHaveCountAsync(0);
		await Expect(Page.GetByText("Login-Serie")).ToHaveCountAsync(0);
		// #1848: must not read as a *weekly* concept confusable with the
		// adjacent "Wochenheld" (weekly-hero-4) badge on the same grid.
		await Expect(Page.GetByText("Aktive Woche")).ToHaveCountAsync(0);

		// #1848: the badge's underlying metric (getMyStreaks().loginStreak) now
		// has a small, secondary indicator on the page too - unlike the other
		// badges' progress metrics (confirmed opportunities, activity streak),
		// it used to be tracked server-side with no visible counter anywhere.
		// SeedLoginStreakForVeraAsync above guarantees this is exactly 1 - a
		// same-day RecordLogin is a no-op past the first call, so the count
		// never drifts regardless of what else runs in this shared session.
		var loginStreakIndicator = Page.GetByTestId("profile-stat-login-streak");
		await Expect(loginStreakIndicator).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(loginStreakIndicator).ToContainTextAsync("1 Tag in Folge angemeldet");
		await Expect(loginStreakIndicator).Not.ToContainTextAsync("Login-Serie");
	}

	[Test]
	public async Task ProfileActivityStreakTile_CarriesAWeekUnit_InsteadOfABareNumber()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// The tile only renders once the volunteer has a non-zero activity
		// streak (ProfileOverviewPage), and UserStreak.RecordActivity only runs
		// on a *confirmed* engagement - seed one rather than depending on some
		// other class in this shared session having incidentally confirmed one
		// for vera first.
		await SeedConfirmedEngagementForVeraAsync(backend);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await SwitchToGermanAsync();

		var tile = Page.GetByTestId("profile-stat-streak");
		await Expect(tile).ToBeVisibleAsync(new() { Timeout = 20_000 });

		// ActivityStreak counts consecutive ISO weeks, and every confirmation in
		// one test session lands in the same week - so this is 1 unless the run
		// straddles a week boundary. Assert the form that matches what actually
		// rendered instead of pinning the count: the regression is the missing
		// unit, and both forms have to carry one.
		var weeks = int.Parse(
			(await tile.Locator("p").First.InnerTextAsync()).Trim(),
			CultureInfo.InvariantCulture);
		await Expect(tile).ToContainTextAsync(weeks == 1 ? "Woche in Serie" : "Wochen in Serie");

		// The bug: the label was the unit-less "Aktivitätsserie", so the tile
		// read as a bare "1" with nothing saying one day, week or shift.
		await Expect(tile).Not.ToContainTextAsync("Aktivitätsserie");
	}

	/// <summary>
	/// Switches the SPA to German. Must run after signing in, never before -
	/// FastSignInAsync itself waits on the English "User menu" aria-label (see
	/// LocalizedCheckInPinErrorTests, which hit this first).
	/// </summary>
	[Test]
	public async Task ProfileStreakStats_ReferenceMatchingBadgeName_ToReadAsDistinct()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// #1935's bug was specifically the co-occurrence of both stats: seed
		// both nonzero at once, the exact scenario the issue describes.
		await SeedLoginStreakForVeraAsync(backend);
		await SeedConfirmedEngagementForVeraAsync(backend);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await SwitchToGermanAsync();

		var streakTile = Page.GetByTestId("profile-stat-streak");
		var loginStreakIndicator = Page.GetByTestId("profile-stat-login-streak");
		await Expect(streakTile).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(loginStreakIndicator).ToBeVisibleAsync(new() { Timeout = 20_000 });

		// Each stat now names its own badge - the week-streak tile backs
		// "Wochenheld" (4 consecutive activity weeks), the day-streak caption
		// backs "Anmeldeserie" (consecutive login days). Cross-check each does
		// NOT carry the other's badge name, not just that it carries its own -
		// that mismatch is exactly the confusion #1935 reported.
		await Expect(streakTile).ToContainTextAsync("Wochenheld");
		await Expect(streakTile).Not.ToContainTextAsync("Anmeldeserie");
		await Expect(loginStreakIndicator).ToContainTextAsync("Anmeldeserie");
		await Expect(loginStreakIndicator).Not.ToContainTextAsync("Wochenheld");
	}

	private async Task SwitchToGermanAsync()
	{
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		// A plain <button> inside the selector's <ul>, not an option: #1825 dropped
		// the listbox/option roles this component never implemented the keyboard
		// model for. Scoped to the open menu so it cannot match anything else.
		await Page.GetByTestId("language-selector-menu")
			.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();
	}

	private async Task SeedLoginStreakForVeraAsync(Uri backend)
	{
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {(await Fixture.SignInAsync("vera", "vera123")).AccessToken}");

		// Any authenticated request would trip LoginStreakMiddleware - this one
		// happens to also be the endpoint the profile page itself calls
		// (GetMyStreaksEndpoint maps "/me/streaks" directly under /v1, not
		// under the /v1/users/... group other user endpoints use).
		(await veraHttp.GetAsync("/v1/me/streaks")).EnsureSuccessStatusCode();
	}

	private async Task SeedConfirmedEngagementForVeraAsync(Uri backend)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {(await Fixture.SignInAsync("olaf", "olaf123")).AccessToken}");

		// Retry-wrapped, not a plain PostAsJsonAsync like the opportunity/
		// engagement/confirm calls below - this is the one call in this method
		// that hits Keycloak's admin API (see PostJsonWithRetryAsync's own doc
		// comment, #1709), and this method now has two callers in this class
		// instead of one.
		var orgResponse = await PostJsonWithRetryAsync(
			olafHttp, "/v1/organizations", new { name = $"AchievementCopy Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"AchievementCopy Opportunity {suffix}",
			descriptionDe = "Created by AchievementCopyTests to give vera an activity streak.",
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
		var opportunityId = opportunity.GetProperty("id").GetString();

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {(await Fixture.SignInAsync("vera", "vera123")).AccessToken}");

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "Ready to help with AchievementCopyTests." });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString();

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
			.EnsureSuccessStatusCode();
	}
}
