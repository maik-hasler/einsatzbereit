using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1930: a city-search result whose label is character-for-
/// character what the visitor just typed (e.g. a real but obscure village
/// literally named the same as the query) used to render identically to
/// every other suggestion - same map-pin icon, same plain text, same
/// `role="option"` styling - giving no signal that it isn't simply the raw
/// typed text echoed back as a fake, selectable "place".
///
/// The underlying behaviour (a genuine geocoded result whose name equals the
/// query) is real Nominatim data, not something this repo's own code
/// produces - see NominatimGeocodingService.ToSuggestions. FakeGeocodingService
/// (wired in for this Aspire stack, see AppHost.cs's "Geocoding__UseFakeService")
/// reproduces it deterministically for one synthetic fixture query rather than
/// depending on a real place name that could rank differently over time.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CityExactNameMatchSuggestionTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Mirrors FakeGeocodingService.ExactMatchFixtureQuery/ExactMatchFixtureOtherResult -
	// not referenced directly since VisualTests has no project reference to
	// Infrastructure.
	private const string ExactMatchFixtureQuery = "Zzztestdorf";
	private const string ExactMatchFixtureOtherResult = "Zzztestwalde";

	[Test]
	public async Task OpportunitiesLocationFilter_ExactNameMatchSuggestion_IsLabeledDistinctlyFromOtherResults()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Page.GetByTestId("filter-location").ClickAsync();

		var cityInput = Page.GetByRole(AriaRole.Combobox, new() { Name = "City" });
		await Expect(cityInput).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await cityInput.FillAsync(ExactMatchFixtureQuery);

		var listbox = Page.GetByRole(AriaRole.Listbox, new() { Name = "City" });
		await Expect(listbox.GetByRole(AriaRole.Option)).ToHaveCountAsync(2, new() { Timeout = 15_000 });

		var exactMatchOption = listbox.GetByRole(AriaRole.Option)
			.Filter(new() { HasText = ExactMatchFixtureQuery });
		var otherOption = listbox.GetByRole(AriaRole.Option)
			.Filter(new() { HasText = ExactMatchFixtureOtherResult });

		// The exact-match option carries a distinguishing caption alongside its
		// label; an unambiguous result (its label doesn't equal what was typed)
		// must not - that contrast is the whole fix, not just the caption's mere
		// presence somewhere on the page.
		await Expect(exactMatchOption).ToContainTextAsync("Exact name match");
		await Expect(otherOption).Not.ToContainTextAsync("Exact name match");
	}
}
