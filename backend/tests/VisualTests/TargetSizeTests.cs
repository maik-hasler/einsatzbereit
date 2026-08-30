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

	// WCAG 2.5.5 (AAA) and every mobile platform guideline put the comfortable
	// touch minimum at 44x44. The drawer rows were 32px tall and the burger 40x40
	// (#2327) - these are a phone's primary navigation, so they are held to that
	// bar rather than to 2.5.8's 24px floor.
	[Test]
	public async Task MobileHeaderAndDrawer_PrimaryControls_MeetThe44pxTouchTarget()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GotoAsync(frontend.ToString());

		var burger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(burger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await AssertMinimumTargetAsync(burger, 44, "the burger toggle");

		await burger.ClickAsync();

		// Both headers are in the DOM at every width - the desktop one is only
		// hidden by CSS - so the language switcher and the sign-in button each
		// exist twice on the page. Address the drawer's copies through the drawer,
		// or Playwright's strict mode rejects the locator.
		var drawer = Page.GetByRole(AriaRole.Dialog, new() { Name = "Menu" });
		await Expect(drawer).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var navRow = drawer.GetByTestId("mobile-nav-findOpportunities");
		await Expect(navRow).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await AssertMinimumTargetAsync(navRow, 44, "a drawer navigation row");

		var languageTrigger = drawer.GetByTestId("language-selector-trigger");
		await Expect(languageTrigger).ToBeVisibleAsync();
		await AssertMinimumTargetAsync(languageTrigger, 44, "the language switcher");

		var signIn = drawer.GetByRole(AriaRole.Button, new() { Name = "Sign in" });
		await Expect(signIn).ToBeVisibleAsync();
		await AssertMinimumTargetAsync(signIn, 44, "the drawer sign-in button");
	}

	[Test]
	public async Task OpportunityCard_TagChip_MeetsTheMinimum24pxTapTarget()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var tag = $"targetsize-{suffix}";

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"TargetSize Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"TargetSize Opportunity {suffix}";
		(await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Seeded by TargetSizeTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
			tags = new[] { tag },
		})).EnsureSuccessStatusCode();

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");

		var chip = Page.GetByRole(AriaRole.Link, new() { Name = $"Filter by tag: {tag}" }).First;
		await Expect(chip).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await AssertMinimumTargetAsync(chip, 24, "an opportunity card's tag chip");
	}

	private static async Task AssertMinimumTargetAsync(ILocator locator, int minimum, string what)
	{
		var box = await locator.BoundingBoxAsync();
		box.Should().NotBeNull($"Could not get bounding box for {what}");

		box!.Height.Should().BeGreaterThanOrEqualTo(minimum,
			$"{what} must be at least {minimum}x{minimum} CSS px (was {box.Width}x{box.Height})");
		box.Width.Should().BeGreaterThanOrEqualTo(minimum,
			$"{what} must be at least {minimum}x{minimum} CSS px (was {box.Width}x{box.Height})");
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
