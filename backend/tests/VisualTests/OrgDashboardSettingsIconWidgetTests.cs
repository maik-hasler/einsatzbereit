using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the SettingsIcon widget's label (#2045). At its own
/// catalog default (compact, 2x1 - see widgetCatalog.ts), the icon+label row
/// used to hide the text span entirely and rely on WidgetCard's own title
/// bar alone - leaving a sighted organizer looking at a bare gear icon with
/// no visible caption on the tile itself, even though the stretched Link
/// already carried an accessible name for assistive tech. The label is now
/// always rendered, regardless of size.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardSettingsIconWidgetTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task CompactDefaultPlacement_ShowsTheSettingsLabel_NotJustTheGearIcon()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual SettingsIcon {Guid.NewGuid():N}");

		// Saves a layout with just the SettingsIcon widget at its own catalog
		// default (2x1, classifyWidth's own compact threshold) directly
		// through the API, rather than driving the "Add Widget" picker in the
		// browser just to reach the same placement.
		using (var http = await CreateAuthenticatedHttpClientAsync(backend))
		{
			var response = await http.PutAsJsonAsync(
				$"/v1/organizations/{organizationId}/dashboard/layout",
				new
				{
					widgets = new[]
					{
						new { widgetKey = "SettingsIcon", x = 1, y = 1, width = 2, height = 1 },
					},
				});
			response.EnsureSuccessStatusCode();
		}

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var settingsIconWidget = Page.GetByTestId("widget-tile-SettingsIcon");
		await Expect(settingsIconWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The decorative icon+label group (aria-hidden - the accessible name
		// comes from the stretched Link) must show the label text on screen,
		// not just carry it as an accessible name nobody sighted can see.
		// Scoped to the <span> tag specifically - WidgetCard's own <h2> title
		// carries the same "Settings" text, and a plain GetByText would match
		// both, which Playwright's strict mode rejects as ambiguous.
		await Expect(settingsIconWidget.Locator("span", new() { HasText = "Settings" }))
			.ToBeVisibleAsync();

		await DeleteOrganizationAsync(backend, organizationId);
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
