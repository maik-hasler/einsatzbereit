using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Playwright;
using TUnit.Core;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SingleMarkerMapTouchScrollTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 390;
	private const int MobileHeight = 844;

	// SingleMarkerMap.tsx's fix for #1664 is `dragging={!L.Browser.mobile}` -
	// Leaflet's own Browser.mobile is UA-string based (contains "mobile"), not
	// derived from the context's HasTouch flag or viewport size. A real
	// mobile Safari UA is needed for the map to actually pick the fixed
	// (dragging-disabled) code path under test.
	private const string MobileUserAgent =
		"Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 "
		+ "(KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

	public override BrowserNewContextOptions ContextOptions(TestContext testContext)
	{
		var options = base.ContextOptions(testContext);
		options.UserAgent = MobileUserAgent;
		options.HasTouch = true;
		options.ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight };
		return options;
	}

	[Test]
	public async Task SingleMarkerMap_OnMobile_DisablesTouchDragSoPageStillScrolls()
	{
		// Regression for #1664: SingleMarkerMap.tsx only disabled
		// scrollWheelZoom (the desktop wheel trap), leaving Leaflet's default
		// touch dragging on - which claims every touch gesture starting on
		// the map (`touch-action: none`) and blocks the page's own vertical
		// swipe-to-scroll. A real device's native touch-scroll suppression
		// can't be reproduced by dispatching synthetic touch events in a test
		// (untrusted events never trigger it), so this asserts the actual
		// mechanism instead: with dragging disabled on mobile, Leaflet's own
		// leaflet.css rule for `leaflet-touch-zoom` alone (no
		// `leaflet-touch-drag`) computes `touch-action: pan-x pan-y`, not
		// `none` - a swipe starting on the map is then free to scroll the
		// page, exactly like the "computed touch-action: none" bug evidence
		// on the issue describes, inverted.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Touch Scroll Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Touch Scroll Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title,
			description = "Created by SingleMarkerMap_OnMobile_DisablesTouchDragSoPageStillScrolls",
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
		// response this page actually reads (the detail fetch) instead of
		// depending on the unreachable real geocoding path.
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

		var hasTouchDragClass = await mapContainer.EvaluateAsync<bool>(
			"el => el.classList.contains('leaflet-touch-drag')");
		hasTouchDragClass.Should().BeFalse(
			"dragging must be disabled on mobile (#1664) - Leaflet's Drag handler "
			+ "only adds leaflet-touch-drag while dragging is enabled");

		var touchAction = await mapContainer.EvaluateAsync<string>(
			"el => getComputedStyle(el).touchAction");
		touchAction.Should().Be("pan-x pan-y",
			"a swipe starting on the map must be able to scroll the page (#1664) - "
			+ "touch-action: none (leaflet-touch-drag + leaflet-touch-zoom together) "
			+ "captures every touch gesture that starts on the element");
	}
}
