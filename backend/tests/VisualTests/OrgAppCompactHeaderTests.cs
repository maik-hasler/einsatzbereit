using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the org app's compact page header (frontend
/// OrgPageHeader.tsx), which replaced the public site's PageHeaderBand inside
/// the org app shell. The band is a marketing surface - a brand-800 stage with
/// 72px display type and a wavy bottom cap - and it pushed the dashboard's
/// first widget roughly half a viewport down the page, on the one screen an
/// organizer opens to find out what is going on right now. The header that
/// replaced it states the organization, the page, its actions and the app's
/// own sections in a fraction of that height.
///
/// The section rail is new here: before this, opportunities/sign-ups/members/
/// settings were reachable only from the avatar dropdown's collapsible submenu
/// or the mobile burger (see OrgAppMobileResponsiveTests, whose own summary is
/// updated accordingly).
/// </summary>
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

		// A freshly created org has no saved layout, so its dashboard renders
		// the default widget set - deterministic regardless of what any other
		// test in this session did to olaf's seeded organizations.
		var organizationId = await CreateOrganizationAsync($"Visual CompactHeader {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Expect(Page.GetByTestId("org-app-header")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The band was the only brand-800 surface ever rendered inside <main>,
		// and the only place the wave motif appeared there.
		await Expect(Page.Locator("main .bg-brand-800")).ToHaveCountAsync(0);
		await Expect(Page.Locator("main svg[viewBox='0 0 1440 60']")).ToHaveCountAsync(0);

		// The page's own title is still a real h1 (axe's page-has-heading-one),
		// just at an app scale rather than the band's 72px display size.
		var heading = Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 });
		await Expect(heading).ToHaveTextAsync("Dashboard");
		var headingFontSizePx = await heading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		headingFontSizePx.Should().BeLessThan(48,
			"the org app states its page title at app scale - the band rendered it at 72px");

		// The point of the change: chrome ends, and content begins, high on the
		// page. The band alone ran ~420px tall plus a ~48px wave cap below the
		// 64px sticky header, so the first widget started past 500px.
		var headerBox = await Page.GetByTestId("org-app-header").BoundingBoxAsync();
		headerBox.Should().NotBeNull();
		(headerBox!.Y + headerBox.Height).Should().BeLessThan(360,
			"the org app header must stay compact - it is chrome above the organizer's actual dashboard");

		var gridBox = await Page.GetByTestId("dashboard-widget-grid").BoundingBoxAsync();
		gridBox.Should().NotBeNull();
		gridBox!.Y.Should().BeLessThan(400,
			"the first widget row must be visible without scrolling on a 900px-tall viewport");

		// The header is opaque here: nothing dark runs behind it in the org app
		// any more, so it must not be left in the band's transparent treatment.
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

		// Every section of the org app is one click away from every other one.
		await Expect(rail.GetByRole(AriaRole.Link)).ToHaveCountAsync(5);
		await Expect(Page.GetByTestId("org-tab-dashboard")).ToHaveAttributeAsync("aria-current", "page");

		await Page.GetByTestId("org-tab-engagements").ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard/engagements",
			new() { Timeout = 15_000 });
		await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToHaveTextAsync("Sign-ups");
		await Expect(Page.GetByTestId("org-tab-engagements")).ToHaveAttributeAsync("aria-current", "page");
		await Expect(Page.GetByTestId("org-tab-dashboard")).Not.ToHaveAttributeAsync("aria-current", "page");

		// The rail survives the narrowest viewport the suite covers - it scrolls
		// horizontally rather than wrapping or pushing the page sideways.
		await Page.SetViewportSizeAsync(375, 812);
		await Expect(rail).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("org-tab-settings")).ToHaveCountAsync(1);
		var railBox = await rail.BoundingBoxAsync();
		railBox.Should().NotBeNull();
		railBox!.Width.Should().BeLessThanOrEqualTo(375,
			"the section rail scrolls inside itself instead of widening the page");

		await Page.SetViewportSizeAsync(1280, 720);
		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Creates an organization through the API with the signed-in user's own
	/// token, so the caller organizes it - same approach as
	/// QuickCheckInWidgetTests, and faster than driving the switcher's
	/// create-organization dialog.
	/// </summary>
	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await http.PostAsJsonAsync("/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	/// <summary>
	/// Live staging accumulates test debris from the shared accounts (see the
	/// root AGENTS.md note) - the same courtesy applies to the Aspire stack
	/// this suite shares across its whole session.
	/// </summary>
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
