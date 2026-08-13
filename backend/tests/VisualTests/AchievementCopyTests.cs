using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1788: three separate copy defects on /profile, all in the
/// German locale that the app actually serves by default.
///
/// 1. The 100-confirmations badge was called "Hundertschaft" - in German first
///    a police/riot-unit term, a jarring association for a civic volunteering
///    product. Renamed to the plain, factual "100 Einsaetze".
/// 2. "Auf Kurs" was described as a "Login-Serie" - Denglish, and it hid the
///    rule behind English loan vocabulary. Renamed "Aktive Woche" with a German
///    description that states the rule it actually measures (seven days in a
///    row); the award rule in RecordLoginCommandHandler is deliberately
///    unchanged, so the description must not start implying volunteering
///    activity the badge does not track either.
/// 3. The activity-streak stat tile rendered a bare number over the unit-less
///    label "Aktivitaetsserie" - one what: day, week, shift? Unlike
///    engagementStatLabel it had no _one/_other forms, so it could not even
///    inflect. UserStreak.ActivityStreak counts consecutive ISO *weeks* with a
///    confirmed engagement, so the label now carries that unit.
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
		var origin = frontend.GetLeftPart(UriPartial.Authority);

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
			.ToHaveTextAsync("Aktive Woche");

		// The description lives in the card for an unearned badge and in the
		// tooltip either way; the tooltip is the one that is always present.
		await Expect(Page.Locator("#badge-tooltip-on-a-roll-7"))
			.ToContainTextAsync("Verdient für sieben Tage in Folge.");

		// Nowhere in the DOM, not merely invisible - the tooltip copy counts too.
		await Expect(Page.GetByText("Hundertschaft")).ToHaveCountAsync(0);
		await Expect(Page.GetByText("Login-Serie")).ToHaveCountAsync(0);
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
	private async Task SwitchToGermanAsync()
	{
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();
	}

	private async Task SeedConfirmedEngagementForVeraAsync(Uri backend)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {(await Fixture.SignInAsync("olaf", "olaf123")).AccessToken}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"AchievementCopy Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"AchievementCopy Opportunity {suffix}",
			description = "Created by AchievementCopyTests to give vera an activity streak.",
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
