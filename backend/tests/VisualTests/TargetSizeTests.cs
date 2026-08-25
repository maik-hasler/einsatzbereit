using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class TargetSizeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	[Arguments(1440, 900)]
	[Arguments(375, 812)]
	public async Task Footer_GitHubLink_MeetsTheMinimum24pxTapTarget(int width, int height)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(width, height);
		await Page.GotoAsync(frontend.ToString());

		var githubLink = Page.GetByRole(AriaRole.Link, new() { Name = "GitHub" });
		await Expect(githubLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var box = await githubLink.BoundingBoxAsync();
		box.Should().NotBeNull("Could not get bounding box for the footer GitHub link");

		box!.Width.Should().BeGreaterThanOrEqualTo(24,
			$"WCAG 2.5.8 requires a 24x24 CSS px minimum target size (was {box.Width}x{box.Height} at {width}px viewport)");
		box.Height.Should().BeGreaterThanOrEqualTo(24,
			$"WCAG 2.5.8 requires a 24x24 CSS px minimum target size (was {box.Width}x{box.Height} at {width}px viewport)");
	}

	[Test]
	public async Task OrgDashboardCalendar_DayNumberButton_MeetsTheMinimum24pxTapTarget()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync(backend, $"Visual TargetSize {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var calendar = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendar).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await calendar.GetByRole(AriaRole.Button, new() { Name = "Month" }).ClickAsync();

		var today = calendar.Locator(".rbc-current .rbc-button-link");
		await Expect(today).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var box = await today.BoundingBoxAsync();
		box.Should().NotBeNull("Could not get bounding box for the calendar's day-number button");

		box!.Width.Should().BeGreaterThanOrEqualTo(24,
			$"WCAG 2.5.8 requires a 24x24 CSS px minimum target size (was {box.Width}x{box.Height})");
		box.Height.Should().BeGreaterThanOrEqualTo(24,
			$"WCAG 2.5.8 requires a 24x24 CSS px minimum target size (was {box.Width}x{box.Height})");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	private async Task<string> CreateOrganizationAsync(Uri backend, string name)
	{
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
