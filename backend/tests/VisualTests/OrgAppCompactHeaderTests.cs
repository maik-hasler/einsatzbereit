using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppCompactHeaderTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgDashboard_HasNoMarketingBand_AndPutsWidgetsHighOnThePage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual CompactHeader {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Expect(Page.GetByTestId("org-app-header")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.Locator("main .bg-brand-800")).ToHaveCountAsync(0);
		await Expect(Page.Locator("main svg[viewBox='0 0 1440 60']")).ToHaveCountAsync(0);

		var heading = Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 });
		await Expect(heading).ToHaveTextAsync("Dashboard");
		var headingFontSizePx = await heading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		headingFontSizePx.Should().BeLessThan(48,
			"the org app states its page title at app scale - the band rendered it at 72px");

		var headerBox = await Page.GetByTestId("org-app-header").BoundingBoxAsync();
		headerBox.Should().NotBeNull();
		(headerBox!.Y + headerBox.Height).Should().BeLessThan(360,
			"the org app header must stay compact - it is chrome above the organizer's actual dashboard");

		var gridBox = await Page.GetByTestId("dashboard-widget-grid").BoundingBoxAsync();
		gridBox.Should().NotBeNull();
		gridBox!.Y.Should().BeLessThan(400,
			"the first widget row must be visible without scrolling on a 900px-tall viewport");

		var headerClass = await Page.Locator("header").GetAttributeAsync("class");
		headerClass.Should().NotBeNull().And.NotContain("bg-transparent");

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task SectionRail_MarksTheCurrentSection_AndNavigatesBetweenThem()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual SectionRail {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var rail = Page.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" });
		await Expect(rail).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(rail.GetByRole(AriaRole.Link)).ToHaveCountAsync(5);
		await Expect(Page.GetByTestId("org-tab-dashboard")).ToHaveAttributeAsync("aria-current", "page");

		await Page.GetByTestId("org-tab-engagements").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard/engagements",
			new() { Timeout = 15_000 });
		await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToHaveTextAsync("Sign-ups");
		await Expect(Page.GetByTestId("org-tab-engagements")).ToHaveAttributeAsync("aria-current", "page");
		await Expect(Page.GetByTestId("org-tab-dashboard")).Not.ToHaveAttributeAsync("aria-current", "page");

		await Page.SetViewportSizeAsync(375, 812);
		await Expect(rail).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("org-tab-settings")).ToHaveCountAsync(1);
		var railBox = await rail.BoundingBoxAsync();
		railBox.Should().NotBeNull();
		railBox!.Width.Should().BeLessThanOrEqualTo(375,
			"the section rail scrolls inside itself instead of widening the page");

		// The fade is driven by a 200ms CSS opacity transition (see OrgPageHeader.tsx),
		// which the viewport resize above just triggered - a single immediate read can
		// still catch it mid-transition, so use Playwright's auto-retrying CSS assertion
		// instead of a one-shot EvaluateAsync read.
		var fadeRight = Page.GetByTestId("org-tabs-fade-right");
		await Expect(fadeRight).ToHaveCSSAsync("opacity", "1",
			new() { Timeout = 5_000 });

		var fadeLeft = Page.GetByTestId("org-tabs-fade-left");
		var fadeLeftOpacity = await fadeLeft.EvaluateAsync<string>("el => getComputedStyle(el).opacity");
		fadeLeftOpacity.Should().Be("0",
			"the rail starts scrolled to its first tab, so there is nothing to scroll back to yet");

		await Page.SetViewportSizeAsync(1280, 720);
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
