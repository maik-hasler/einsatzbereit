using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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

		await Expect(settingsIconWidget.Locator("span", new() { HasText = "Settings" }))
			.ToBeVisibleAsync();

		await DeleteOrganizationAsync(backend, organizationId);
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
