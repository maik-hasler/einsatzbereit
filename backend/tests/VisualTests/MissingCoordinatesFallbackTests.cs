using System.Net.Http.Json;
using System.Text.Json;
using TUnit.Core;

namespace VisualTests;

// #1963: a non-remote opportunity whose address hasn't (yet) resolved to
// coordinates used to omit the map section entirely, with no acknowledgment
// either way - two offers from the same organization could render the detail
// page inconsistently depending on hidden geocoding success. A "no map
// available" placeholder (the same size as the map it stood in for) fixed
// that, but reserved map-sized space to restate the address that was already
// printed as text in the "Where" fact just above it (#2058). The placeholder
// is now gone entirely - the address stays promoted where it already was,
// and the directions link (missing for every opportunity before #2058, with
// or without coordinates) still gives a text-address-based escape hatch even
// when there is no pin to link to directly.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MissingCoordinatesFallbackTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunityDetailPage_CollapsesMapSection_AndLinksDirectionsByAddress_WhenCoordinatesAreMissing()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"No Map Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"No Map Test {suffix}";

		// VisualTests always runs against FakeGeocodingService (AppHost.cs's
		// Geocoding__UseFakeService override), which reports TransientFailure -
		// this opportunity is created with null coordinates as a result, same
		// as it would be while geocoding is still pending in production.
		var oppResponse = await PostJsonWithRetryAsync(http, "/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by OpportunityDetailPage_CollapsesMapSection_AndLinksDirectionsByAddress_WhenCoordinatesAreMissing",
			organizationId,
			isRemote = false,
			street = "Teststrasse",
			houseNumber = "1",
			zipCode = "12345",
			city = "Musterstadt",
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });

		await Expect(Page.GetByTestId("map-unavailable")).Not.ToBeAttachedAsync();
		await Expect(Page.Locator(".leaflet-container")).Not.ToBeAttachedAsync();

		var directionsLink = Page.GetByTestId("opportunity-directions-link");
		await Expect(directionsLink).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(directionsLink).ToHaveAttributeAsync(
			"href",
			"https://www.google.com/maps/dir/?api=1&destination=Teststrasse%201%2C%2012345%20Musterstadt");
	}
}
