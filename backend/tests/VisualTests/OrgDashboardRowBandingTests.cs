using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #1932: a saved layout that never reached the full
/// GRID_COLUMNS width on some of its rows (most commonly a lightly-
/// customized layout that trimmed a widget down without ever widening it
/// back out) used to render inside a container that always spanned the
/// full page width regardless - leaving a permanent blank block next to
/// whichever rows fell short, reading as unfinished on a wide viewport.
/// OrgDashboardPage/index.tsx now splits the layout into independent row
/// bands (groupIntoRowBands in widgetCatalog.ts) and caps each band's own
/// container to the width its own widgets actually reach, so a narrow
/// band's container ends right where its widgets end instead of always
/// spanning the page - see widgetCatalog.test.ts for the split logic
/// itself; this covers the resulting rendered layout.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardRowBandingTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NarrowStandaloneWidget_RendersInABandCappedToItsOwnWidth_NotTheFullGrid()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual RowBandNarrow {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// UpcomingOpportunities lands at (x=1, y=1, width=4, height=2) - see
		// placeNewWidget in widgetCatalog.ts - the only widget on the grid,
		// reaching column 4 of the 8-column grid.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-UpcomingOpportunities").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var tile = Page.GetByTestId("widget-tile-UpcomingOpportunities");
		await Expect(tile).ToBeVisibleAsync();

		// The tile's own immediate parent is its row band's own grid
		// container (OrgDashboardPage/index.tsx) - if the band is correctly
		// capped to this lone widget's own reach, the band's rendered width
		// should be (close to) the tile's own width, not the full
		// page-width grid the tile would otherwise only span half of.
		double parentWidth = 0, tileWidth = 0;
		await PollUntilAsync(async () =>
		{
			var widths = await tile.EvaluateAsync<double[]>(
				"el => [el.parentElement.getBoundingClientRect().width, el.getBoundingClientRect().width]");
			parentWidth = widths[0];
			tileWidth = widths[1];
			return tileWidth > 0;
		}, () => "UpcomingOpportunities tile never reported a non-zero width");

		Math.Abs(parentWidth - tileWidth).Should().BeLessThan(20,
			"a standalone narrow widget's own row band should be capped to its own width, not the "
			+ $"full page-width grid (tile width: {tileWidth}px, parent/band width: {parentWidth}px)");

		// And that band really is capped, not just coincidentally as wide as
		// the tile for some other reason - it should be meaningfully
		// narrower than the page's own content column (~1376px at this
		// viewport, see --container-page in global.css).
		parentWidth.Should().BeLessThan(800,
			"the band should be capped to roughly half the grid's width (UpcomingOpportunities is "
			+ "4 of GRID_COLUMNS=8) rather than spanning the full ~1376px content column "
			+ $"(last observed: {parentWidth}px)");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task MixedLayout_CapsOnlyTheNarrowBand_AndLeavesTheFullWidthRowUncapped()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual RowBandMixed {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Settings lands first at (x=1, y=1, width=8, height=1) - full
		// width. VolunteerStats is added next and lands right below it, at
		// (x=1, y=2, width=4, height=1) - see placeNewWidget in
		// widgetCatalog.ts - in its own separate, narrower band, since the
		// two don't share any row.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-option-VolunteerStats").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var settingsTile = Page.GetByTestId("widget-tile-Settings");
		var statsTile = Page.GetByTestId("widget-tile-VolunteerStats");
		await Expect(settingsTile).ToBeVisibleAsync();
		await Expect(statsTile).ToBeVisibleAsync();

		double settingsParentWidth = 0, statsParentWidth = 0, statsTileWidth = 0;
		await PollUntilAsync(async () =>
		{
			var settingsWidths = await settingsTile.EvaluateAsync<double[]>(
				"el => [el.parentElement.getBoundingClientRect().width, el.getBoundingClientRect().width]");
			var statsWidths = await statsTile.EvaluateAsync<double[]>(
				"el => [el.parentElement.getBoundingClientRect().width, el.getBoundingClientRect().width]");
			settingsParentWidth = settingsWidths[0];
			statsParentWidth = statsWidths[0];
			statsTileWidth = statsWidths[1];
			return settingsWidths[1] > 0 && statsTileWidth > 0;
		}, () => "Settings/VolunteerStats tiles never reported a non-zero width");

		// The full-width Settings row's own band needs no capping, so its
		// container renders visibly wider than VolunteerStats' own capped
		// one - a single shared full-width grid behind both (the pre-#1932
		// behavior) would make the two equal instead.
		(settingsParentWidth - statsParentWidth).Should().BeGreaterThan(200,
			"the full-width Settings band and the narrower VolunteerStats band should render at "
			+ $"visibly different widths (Settings band: {settingsParentWidth}px, "
			+ $"VolunteerStats band: {statsParentWidth}px)");
		Math.Abs(statsParentWidth - statsTileWidth).Should().BeLessThan(20,
			"VolunteerStats' own band should be capped to its own width, not left spanning the same "
			+ $"full-width grid as the Settings row above it (tile: {statsTileWidth}px, "
			+ $"band: {statsParentWidth}px)");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Same per-widget removal loop as OrgDashboardCustomizeTests - kept
	/// local rather than shared, matching how each VisualTests class in
	/// this suite already owns its own copy of this kind of setup helper.
	/// </summary>
	private async Task RemoveAllWidgetsAsync()
	{
		foreach (var (testId, widgetTitle) in new[]
		{
			("CreateOpportunity", "Create opportunity"),
			("ToDo", "Needs your attention"),
			("VolunteerStats", "Volunteers"),
			("UpcomingOpportunities", "Upcoming opportunities"),
			("Calendar", "Calendar"),
			("Settings", "Organization"),
		})
		{
			var tile = Page.GetByTestId($"widget-tile-{testId}");
			if (await tile.CountAsync() == 0)
				continue;
			await tile
				.GetByRole(AriaRole.Button, new() { Name = $"Remove {widgetTitle} widget" })
				.ClickAsync();
		}
	}

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
