using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1340: the privacy policy claimed "no data is passed on to third parties"
/// while the app silently sends every visitor's IP address to OpenStreetMap's
/// map-tile servers (SingleMarkerMap) and, on top of that, the search term
/// typed into the city filter to the public Nominatim geocoder (useCitySuggestions)
/// - both without disclosure. These tests pin the new disclosure subsection so
/// the contradiction can't silently regress.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class PrivacyPolicyDisclosureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task PrivacyPolicyPage_DisclosesOpenStreetMapAndNominatimThirdPartyTransfers()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Map display and location search (OpenStreetMap, Nominatim)" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("legitimate interest", new() { Exact = false }))
			.ToBeVisibleAsync();

		var osmLink = Page.GetByRole(AriaRole.Link,
			new() { Name = "OpenStreetMap Foundation Privacy Policy" });
		await Expect(osmLink).ToBeVisibleAsync();
		await Expect(osmLink).ToHaveAttributeAsync(
			"href", "https://wiki.osmfoundation.org/wiki/Privacy_Policy");
		await Expect(osmLink).ToHaveAttributeAsync("target", "_blank");
		await Expect(osmLink).ToHaveAttributeAsync("rel", "noopener noreferrer");

		var nominatimLink = Page.GetByRole(AriaRole.Link,
			new() { Name = "Nominatim Usage Policy" });
		await Expect(nominatimLink).ToBeVisibleAsync();
		await Expect(nominatimLink).ToHaveAttributeAsync(
			"href", "https://operations.osmfoundation.org/policies/nominatim/");

		// Regression guard for the exact contradiction reported in #1340: the
		// data-sharing section must no longer make an unqualified "not passed
		// on to third parties" claim now that it explicitly carves out the
		// map/geocoding services documented above.
		await Expect(Page.GetByText(
			"Your personal data will not be passed on to third parties unless",
			new() { Exact = false })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task PrivacyPolicyPage_DisclosesThirdPartyTransfers_InGerman()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "Kartendarstellung und Ortssuche (OpenStreetMap, Nominatim)" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("berechtigtes Interesse", new() { Exact = false }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link,
			new() { Name = "Datenschutzerklärung der OpenStreetMap Foundation" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link,
			new() { Name = "Nutzungsrichtlinie von Nominatim" }))
			.ToBeVisibleAsync();

		await Expect(Page.GetByText(
			"Eine Übermittlung Ihrer persönlichen Daten an Dritte findet nicht statt,",
			new() { Exact = false })).Not.ToBeVisibleAsync();
	}
}
