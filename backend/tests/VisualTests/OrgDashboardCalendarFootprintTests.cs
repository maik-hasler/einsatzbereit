using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the Calendar widget's footprint in the shipped default
/// dashboard layout (DEFAULT_LAYOUT in frontend widgetCatalog.ts).
///
/// #1795: the Calendar shipped 8 columns wide and <b>6 rows</b> tall, and the
/// grid's row height tracks its rendered column width (see
/// .dashboard-widget-grid in global.css), so on a 1440px screen the first
/// thing an organizer saw on their dashboard was ~900px of month grid holding
/// a couple of event bars. It is now 4 rows - its own catalog minHeight, and
/// still well above CalendarWidget's 400px internal floor, so the month view
/// it opens on at full width keeps rendering legibly.
///
/// Only organizations that never customized their dashboard are affected: the
/// page falls back to DEFAULT_LAYOUT solely when the API reports
/// hasCustomLayout=false, and nothing migrates a stored layout - the second
/// test here is the guard on that.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardCalendarFootprintTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task DefaultLayout_GivesTheCalendarAProportionateFootprint_AndLeavesNoGapBelowIt()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A freshly created org has no saved layout, so its dashboard renders
		// DEFAULT_LAYOUT - deterministic regardless of what any other test in
		// this session did to olaf's seeded organizations.
		var organizationId = await CreateOrganizationAsync($"Visual CalFootprint {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The stored rect itself, straight off the tile's inline grid placement
		// (index.tsx writes `${y} / span ${height}`) - the exact thing #1795
		// changed, asserted without depending on any pixel measurement.
		(await calendar.EvaluateAsync<string>("el => el.style.gridRow"))
			.Should().Be("4 / span 4", "the Calendar defaults to 4 rows, not the 6 it shipped with");
		(await Page.GetByTestId("widget-tile-Settings").EvaluateAsync<string>("el => el.style.gridRow"))
			.Should().Be("8 / span 1",
				"shrinking the Calendar must pull Settings up with it, not leave three empty rows");

		// UpcomingOpportunities is 2 rows tall, so a 4-row Calendar renders
		// roughly twice its height (rows plus one extra gap). Expressed as a
		// ratio rather than a pixel count because the row height is a container
		// query on the grid's own rendered width - at 6 rows this was ~3.1.
		var upcomingBox = await Page.GetByTestId("widget-tile-UpcomingOpportunities").BoundingBoxAsync();
		var calendarBox = await calendar.BoundingBoxAsync();
		upcomingBox.Should().NotBeNull();
		calendarBox.Should().NotBeNull();
		((double)calendarBox!.Height / upcomingBox!.Height).Should().BeLessThan(2.5,
			"the Calendar's height must stay proportionate to the widgets around it");

		// Acceptance criterion: the widgets carrying actionable information are
		// on the first screen of a 900px-tall viewport.
		foreach (var testId in new[] { "CreateOpportunity", "ToDo", "UpcomingOpportunities" })
		{
			var box = await Page.GetByTestId($"widget-tile-{testId}").BoundingBoxAsync();
			box.Should().NotBeNull();
			(box!.Y + box.Height).Should().BeLessThan(900,
				$"the {testId} widget must be fully above the fold at 1440x900");
		}

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task SavedLayout_KeepsItsOwnCalendarHeight_AndIsNotResetToTheNewDefault()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CalSavedLayout {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		// Keyboard corner placement (same state machine as a mouse click on two
		// grid cells, see OrgDashboardCustomizeTests): the cursor starts on the
		// Calendar's own top-left corner at (x=1, y=4), the second Enter locks
		// it there, then 7x ArrowRight + 4x ArrowDown walks to (col=8, row=8)
		// before the last Enter commits - keeping the full width but making the
		// widget 5 rows tall, deliberately different from the new default of 4.
		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Calendar" });
		await moveButton.FocusAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Enter");
		for (var i = 0; i < 7; i++)
			await Page.Keyboard.PressAsync("ArrowRight");
		for (var i = 0; i < 4; i++)
			await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Enter");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The stored 5 rows survive: this organization has hasCustomLayout=true
		// now, so DEFAULT_LAYOUT - whatever it currently says - never applies
		// to it again.
		string? gridRow = null;
		await PollUntilAsync(async () =>
		{
			gridRow = await calendar.EvaluateAsync<string>("el => el.style.gridRow");
			return gridRow == "4 / span 5";
		}, () => "a saved layout must keep its own Calendar height rather than being reset to the "
			+ $"default 4 rows (last observed: \"{gridRow}\")", timeoutMs: 10_000);

		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Creates an organization through the API with the signed-in user's own
	/// token, so the caller organizes it - same approach as
	/// OrgAppCompactHeaderTests, and faster than driving the switcher's
	/// create-organization dialog.
	/// </summary>
	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await http.PostAsJsonAsync("/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private async Task DeleteOrganizationAsync(Uri backend, string organizationId)
	{
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		await http.DeleteAsync($"/v1/organizations/{organizationId}");
	}

	private async Task<HttpClient> CreateAuthenticatedHttpClientAsync(Uri backend)
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		return http;
	}
}
