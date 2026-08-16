using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1962: a `city`-only deep link to /opportunities (bookmarked,
/// shared, or hand-edited, e.g. `?city=Leipzig`) used to leave the "Location"
/// filter chip inactive and show every opportunity unfiltered, even though the
/// visitor's own URL clearly asked for a location filter - `hasLocation` only
/// ever looked at `lat`/`lng`/`radius`, never `city` alone, and nothing tried
/// to resolve the bare city name into coordinates.
///
/// The Aspire stack under test wires FakeGeocodingService in place of the real
/// Nominatim-backed geocoder (see FakeGeocodingService.cs, AppHost.cs's
/// `Geocoding__UseFakeService`), which returns no results for SearchCitiesAsync
/// for every query except its own #1930 regression fixture (not "Leipzig") -
/// so this exercises the fix's "city can't be resolved"
/// fallback deterministically: the chip must still surface the typed city as
/// an active filter rather than reverting to blank/unfiltered, per the
/// issue's own "at minimum" acceptance bar, and clearing it must be able to
/// get back to the unfiltered list.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CityOnlyDeepLinkLocationFilterTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunitiesPage_CityOnlyDeepLink_ShowsLocationChipActiveWithTypedCity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities?city=Leipzig");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var locationChip = Page.GetByTestId("filter-location");
		await Expect(locationChip).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(locationChip).ToHaveTextAsync("Leipzig", new() { Timeout = 15_000 });

		// The chip's own clear (X) button only renders while FilterDropdown
		// considers it active (`active = !!displayValue`) - its presence is
		// itself part of what this asserts, not just setup for what follows.
		var clearLocationButton = Page.GetByRole(AriaRole.Button, new() { Name = "Clear location filter" });
		await Expect(clearLocationButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// hasFilters must also count the still-unresolved city, so the visitor
		// has a "Reset" button back to the unfiltered list rather than one that
		// looks like nothing is filtered while the chip disagrees.
		var resetButton = Page.GetByRole(AriaRole.Button, new() { Name = "Reset" });
		await Expect(resetButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await clearLocationButton.ClickAsync();

		await Expect(locationChip).ToHaveTextAsync("Location", new() { Timeout = 15_000 });
		Page.Url.Should().NotContain("city=Leipzig");
	}
}
