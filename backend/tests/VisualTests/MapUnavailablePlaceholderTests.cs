using System.Net.Http.Json;
using System.Text.Json;
using TUnit.Core;

namespace VisualTests;

// #1963: a non-remote opportunity whose address hasn't (yet) resolved to
// coordinates used to omit the map section entirely, with no acknowledgment
// either way - two offers from the same organization could render the detail
// page inconsistently depending on hidden geocoding success. Now a
// "no map available" note takes its place instead.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MapUnavailablePlaceholderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunityDetailPage_ShowsMapUnavailableNote_WhenCoordinatesAreMissing()
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
			title,
			description = "Created by OpportunityDetailPage_ShowsMapUnavailableNote_WhenCoordinatesAreMissing",
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

		await Expect(Page.GetByTestId("map-unavailable")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator(".leaflet-container")).Not.ToBeAttachedAsync();
	}
}
