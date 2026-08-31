using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-UpcomingOpportunities").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var tile = Page.GetByTestId("widget-tile-UpcomingOpportunities");
		await Expect(tile).ToBeVisibleAsync();

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

		parentWidth.Should().BeLessThan(700,
			"the band should be capped to roughly three eighths of the grid's width "
			+ "(UpcomingOpportunities is 3 of GRID_COLUMNS=8) rather than spanning the full "
			+ $"~1376px content column (last observed: {parentWidth}px)");

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

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Calendar").ClickAsync();
		await dialog.GetByTestId("add-widget-option-VolunteerStats").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var calendarTile = Page.GetByTestId("widget-tile-Calendar");
		var statsTile = Page.GetByTestId("widget-tile-VolunteerStats");
		await Expect(calendarTile).ToBeVisibleAsync();
		await Expect(statsTile).ToBeVisibleAsync();

		double calendarParentWidth = 0, statsParentWidth = 0, statsTileWidth = 0;
		await PollUntilAsync(async () =>
		{
			var calendarWidths = await calendarTile.EvaluateAsync<double[]>(
				"el => [el.parentElement.getBoundingClientRect().width, el.getBoundingClientRect().width]");
			var statsWidths = await statsTile.EvaluateAsync<double[]>(
				"el => [el.parentElement.getBoundingClientRect().width, el.getBoundingClientRect().width]");
			calendarParentWidth = calendarWidths[0];
			statsParentWidth = statsWidths[0];
			statsTileWidth = statsWidths[1];
			return calendarWidths[1] > 0 && statsTileWidth > 0;
		}, () => "Calendar/VolunteerStats tiles never reported a non-zero width");

		(calendarParentWidth - statsParentWidth).Should().BeGreaterThan(200,
			"the full-width Calendar band and the narrower VolunteerStats band should render at "
			+ $"visibly different widths (Calendar band: {calendarParentWidth}px, "
			+ $"VolunteerStats band: {statsParentWidth}px)");
		Math.Abs(statsParentWidth - statsTileWidth).Should().BeLessThan(20,
			"VolunteerStats' own band should be capped to its own width, not left spanning the same "
			+ $"full-width grid as the Calendar rows above it (tile: {statsTileWidth}px, "
			+ $"band: {statsParentWidth}px)");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	private async Task RemoveAllWidgetsAsync()
	{
		foreach (var (testId, widgetTitle) in new[]
		{
			("CreateOpportunity", "Quick actions"),
			("ToDo", "Sign-ups to review"),
			("VolunteerStats", "Volunteers"),
			("UpcomingOpportunities", "What's next"),
			("Calendar", "Calendar"),
			("Settings", "Team"),
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
