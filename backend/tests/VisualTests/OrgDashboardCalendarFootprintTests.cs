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
///
/// #2045 stopped forcing every widget in a shared row to one uniform,
/// square-derived height outside edit mode - see the third test here for the
/// week-view/empty-grid half of that fix, and .dashboard-widget-grid--editing
/// in global.css for the CSS itself.
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

		// #2045 stopped forcing every row in a shared band to one uniform,
		// container-query-derived square height outside edit mode (see
		// .dashboard-widget-grid--editing in global.css) - a fresh org's empty
		// UpcomingOpportunities and empty-Agenda Calendar now each size to
		// their own short content instead of both being stretched to the same
		// multi-row square, so comparing the two against each other no longer
		// says anything about whether the Calendar itself is bloated. What
		// still catches a real regression (back toward the ~900px, 6-row
		// footprint #1795 fixed) is the Calendar's own absolute height,
		// bounded well above its 400px internal floor (CALENDAR_MIN_HEIGHT_PX)
		// but nowhere near that old size.
		var calendarBox = await calendar.BoundingBoxAsync();
		calendarBox.Should().NotBeNull();
		calendarBox!.Height.Should().BeLessThan(700,
			"the Calendar must stay close to its own content/floor height, not balloon back toward the "
				+ "~900px footprint #1795 fixed");

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

	[Test]
	public async Task CustomizedMediumWidthCalendar_WithNoEventsInTheDefaultWeek_FallsBackToAgenda()
	{
		// #2045: a medium-width placement (classifyWidth in widgetCatalog.ts)
		// defaults CalendarWidget to week view (defaultViewForSize), which -
		// before this fix - stayed on whatever week `new Date()` fell in even
		// when that week held nothing at all, rendering a completely empty
		// grid scrolled to midnight. This was the exact bug reported: "the
		// org's only upcoming opportunity falls outside the displayed week".
		// The fix generalizes the existing month-only empty-view fallback
		// (#983) to any grid view, so an empty week now lands on Agenda
		// instead - which has no "which week" problem since it just lists
		// whatever is actually upcoming, however far out that is.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CalEmptyWeek {Guid.NewGuid():N}");

		// Saves a layout with just a medium-width (5 columns, classifyWidth's
		// own <=5 threshold) Calendar directly through the API, rather than
		// driving the corner-to-corner resize UI in the browser just to reach
		// the same placement - this org has no events at all, so which exact
		// week `new Date()` lands in doesn't matter.
		using (var http = await CreateAuthenticatedHttpClientAsync(backend))
		{
			var response = await http.PutAsJsonAsync(
				$"/v1/organizations/{organizationId}/dashboard/layout",
				new
				{
					widgets = new[]
					{
						new { widgetKey = "Calendar", x = 1, y = 1, width = 5, height = 4 },
					},
				});
			response.EnsureSuccessStatusCode();
		}

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Never sits on the empty time-grid view it defaulted to. Agenda's own
		// empty state (Agenda.js) renders a bare "no events" span with no
		// .rbc-agenda-table at all - .rbc-agenda-view is the wrapper present
		// either way, so that's what confirms the view itself switched.
		await Expect(calendarWidget.Locator(".rbc-time-view")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(calendarWidget.Locator(".rbc-agenda-view")).ToBeVisibleAsync();
		await Expect(calendarWidget.GetByText("No events in this range.")).ToBeVisibleAsync();

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
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name });
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
