using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CalendarToolbarContrastTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const double MinimumTextContrastRatio = 4.5;

	// react-big-calendar's own stylesheet paints a hovered toolbar button #373a3c
	// from a selector one pseudo-class more specific than the app's resting
	// `.rbc-active` rule, so the selected view button kept the brand-green
	// background and took the library's dark grey label - 1.06:1, reported by axe
	// as `color-contrast` at serious impact (#2327). Only a hover in a real
	// browser reaches that state, which is why it lives here.
	[Test]
	public async Task OrgDashboardCalendar_SelectedViewButton_StaysReadableWhileHovered()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var response = await PostJsonWithRetryAsync(
			http, "/v1/organizations", new { name = $"Calendar Contrast {Guid.NewGuid():N}" });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var selectedView = calendar.Locator(".rbc-btn-group button.rbc-active");
		await Expect(selectedView).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await selectedView.HoverAsync();

		var (foreground, background) = await ComputedColorsAsync(selectedView);
		ContrastRatio(foreground, background).Should().BeGreaterThanOrEqualTo(
			MinimumTextContrastRatio,
			$"the selected calendar view button must stay readable while hovered (was {foreground} on {background})");

		await http.DeleteAsync($"/v1/organizations/{organizationId}");
	}

	private static async Task<(string Foreground, string Background)> ComputedColorsAsync(ILocator locator)
	{
		var colors = await locator.EvaluateAsync<string[]>(
			"el => { const s = getComputedStyle(el); return [s.color, s.backgroundColor]; }");
		return (colors[0], colors[1]);
	}

	private static double ContrastRatio(string foreground, string background)
	{
		var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
		var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
		return (lighter + 0.05) / (darker + 0.05);
	}

	private static double RelativeLuminance(string cssColor)
	{
		var channels = cssColor
			.Split('(', ')')[1]
			.Split(',')
			.Take(3)
			.Select(part => double.Parse(part.Trim(), CultureInfo.InvariantCulture))
			.ToArray();

		channels.Should().HaveCount(3, $"'{cssColor}' must be an rgb()/rgba() colour");

		return 0.2126 * ToLinear(channels[0]) + 0.7152 * ToLinear(channels[1]) + 0.0722 * ToLinear(channels[2]);
	}

	private static double ToLinear(double channel)
	{
		var c = channel / 255.0;
		return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
	}
}
