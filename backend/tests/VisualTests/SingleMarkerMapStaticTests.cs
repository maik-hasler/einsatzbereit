using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Playwright;
using TUnit.Core;

namespace VisualTests;

/// <summary>
/// The opportunity-detail page's map is a fixed, static view of the
/// opportunity's location, not a browsable map - it used to allow
/// dragging/panning on desktop (`dragging={!L.Browser.mobile}` only disabled
/// it on mobile, see SingleMarkerMapTouchScrollTests.cs's #1664 fix).
/// SingleMarkerMap.tsx now disables every Leaflet interaction unconditionally
/// (dragging, all zoom modes, keyboard) and drops the zoom control.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SingleMarkerMapStaticTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task SingleMarkerMap_OnDesktop_DoesNotPanWhenDragged()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Static Map Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Static Map Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by SingleMarkerMap_OnDesktop_DoesNotPanWhenDragged",
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

		// VisualTests always runs against FakeGeocodingService (AppHost.cs's
		// Geocoding__UseFakeService override), which reports TransientFailure
		// so no seeded opportunity here ever gets real coordinates - the map
		// would never render otherwise. Patch coordinates into the one
		// response this page actually reads (the detail fetch), same as
		// SingleMarkerMapTouchScrollTests.cs.
		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			body["latitude"] = JsonValue.Create(52.52);
			body["longitude"] = JsonValue.Create(13.405);

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });

		var mapContainer = Page.Locator(".leaflet-container");
		await Expect(mapContainer).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// zoomControl={false}: there's nothing left for the +/- buttons to do
		// once every zoom mode is disabled, so the control isn't rendered.
		await Expect(Page.Locator(".leaflet-control-zoom")).Not.ToBeAttachedAsync();

		var mapPane = Page.Locator(".leaflet-map-pane");
		var transformBefore = await mapPane.EvaluateAsync<string>("el => el.style.transform");

		var box = await mapContainer.BoundingBoxAsync();
		box.Should().NotBeNull();
		var centerX = box!.X + (box.Width / 2);
		var centerY = box.Y + (box.Height / 2);

		// A plain mouse drag used to pan the map on desktop -
		// `dragging={!L.Browser.mobile}` only ever disabled it on mobile.
		await Page.Mouse.MoveAsync(centerX, centerY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(centerX + 120, centerY + 80, new() { Steps = 10 });
		await Page.Mouse.UpAsync();

		var transformAfter = await mapPane.EvaluateAsync<string>("el => el.style.transform");
		transformAfter.Should().Be(transformBefore,
			"dragging must be fully disabled - the detail page's map is a fixed, "
			+ "static view of the opportunity's location, not a pannable map");
	}

	[Test]
	public async Task SingleMarkerMap_AttributionLinks_UseBrandColor()
	{
		// #2058: Leaflet ships no color for its attribution links, so they
		// rendered in Leaflet's own default blue instead of the brand green
		// used everywhere else in the app. #226947 (brand-700) is rgb(34, 105, 71).
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Attribution Color Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Attribution Color Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by SingleMarkerMap_AttributionLinks_UseBrandColor",
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

		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			body["latitude"] = JsonValue.Create(52.52);
			body["longitude"] = JsonValue.Create(13.405);

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });

		var attributionLink = Page.Locator(".leaflet-control-attribution a").First;
		await Expect(attributionLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var color = await attributionLink.EvaluateAsync<string>("el => getComputedStyle(el).color");
		color.Should().Be("rgb(34, 105, 71)");
	}

	[Test]
	public async Task SingleMarkerMap_DirectionsLink_UsesCoordinates_WhenAvailable()
	{
		// #2058: before this, there was no escape hatch off the static,
		// unpannable map anywhere on the page - a visitor had to copy the
		// address by hand into another app. With coordinates available the
		// directions link should go straight to them rather than a re-geocode
		// of the text address.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Directions Link Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Directions Link Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by SingleMarkerMap_DirectionsLink_UsesCoordinates_WhenAvailable",
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

		await Page.RouteAsync($"**/v1/volunteer-opportunities/{opportunityId}", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			var response = await route.FetchAsync();
			var body = JsonNode.Parse(await response.TextAsync())!.AsObject();
			body["latitude"] = JsonValue.Create(52.52);
			body["longitude"] = JsonValue.Create(13.405);

			await route.FulfillAsync(new()
			{
				Response = response,
				ContentType = "application/json",
				Body = body.ToJsonString(),
			});
		});

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Expect(Page.Locator("h1").First).ToHaveTextAsync(title, new() { Timeout = 15_000 });

		var directionsLink = Page.GetByTestId("opportunity-directions-link");
		await Expect(directionsLink).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(directionsLink).ToHaveAttributeAsync(
			"href", "https://www.google.com/maps/dir/?api=1&destination=52.52,13.405");
		await Expect(directionsLink).ToHaveAttributeAsync("target", "_blank");
	}
}
