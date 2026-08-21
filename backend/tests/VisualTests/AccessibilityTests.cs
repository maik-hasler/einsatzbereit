using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Page-level accessibility gate.
///
/// This file used to hold every automated a11y check in the project - 85 axe
/// scans, each booting the Aspire stack and driving a browser to reach a
/// component that could have been rendered directly. Issue #2148 moved the
/// component-level scans down to <c>vitest-axe</c> in
/// <c>frontend/src/**/*.a11y.test.tsx</c>, which run in-process against jsdom
/// in seconds. What is left here is what only a real browser can answer:
///
/// <list type="bullet">
/// <item><description><b>Page composition</b> - landmarks, heading order and
/// document structure are properties of a whole page, not of any one
/// component, and axe skips its page-level rules entirely when handed a
/// fragment.</description></item>
/// <item><description><b>Colour contrast</b> - axe samples rendered pixels
/// through a canvas. jsdom has neither layout nor canvas, so the component
/// suite can only ever report contrast "incomplete". These scans are the only
/// place it is genuinely evaluated.</description></item>
/// <item><description><b>Real focus and pointer behaviour</b> - the two skip
/// links moving focus into <c>#main-content</c>, the Leaflet marker's
/// accessible name, DOM order deciding which control a Tab reaches
/// first.</description></item>
/// </list>
///
/// One scan per distinct layout and palette (public site, signed-in volunteer,
/// org app shell, administration, static legal page), not one per state: a
/// state that differs only in which component is mounted is covered by that
/// component's own suite. Add a page-level scan here when a new <i>route</i>
/// appears; add a component suite in <c>frontend/</c> when a new component or
/// component state does.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccessibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Axe reports "page has no h1" (page-has-heading-one) and most
	// landmark-structure defects at "moderate" impact, which the
	// serious/critical filter below would let through. Escalate just these
	// rule IDs rather than every moderate violation, which would also flag
	// color-contrast-enhanced noise unrelated to this gate's purpose.
	private static readonly string[] EscalatedModerateRuleIds =
	[
		"page-has-heading-one",
		"heading-order",
		"landmark-one-main",
		"landmark-banner-is-top-level",
		"landmark-complementary-is-top-level",
		"landmark-contentinfo-is-top-level",
		"landmark-main-is-top-level",
		"landmark-no-duplicate-banner",
		"landmark-no-duplicate-contentinfo",
		"landmark-no-duplicate-main",
		"landmark-unique",
		// "region" (also moderate) is deliberately NOT escalated: axe's region
		// rule flags any visible content outside a landmark, and ToastContext.tsx
		// mounts its toast list at the app root, outside AppLayout's <main>.
		// Escalating it would fail every scan that catches a toast or similar
		// page-root overlay mid-render.
	];

	private static void AssertNoViolations(AxeResult result)
	{
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical"
				|| (v.Impact is "moderate" && EscalatedModerateRuleIds.Contains(v.Id)))
			.ToList();

		if (violations.Count == 0)
			return;

		var summary = string.Join("\n", violations.Select(v =>
			$"[{v.Impact}] {v.Id}: {v.Description}\n" +
			string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));

		throw new Exception($"Axe found {violations.Count} a11y violation(s):\n{summary}");
	}

	[Test]
	public async Task HomePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task HomePage_SkipLink_MovesFocusToMainContent()
	{
		// Neither layout had a bypass mechanism - a keyboard
		// user had to tab through the entire header (brand link, nav links,
		// language selector, sign-in/register) on every single page before this.
		// The skip link is the first child in the DOM (before <Header>), so it
		// must also be the very first Tab stop.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Wait for opportunity cards (not footer links which also match ul>li a)
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no opportunities seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_SignUpCta_IsReachableBeforeReportButton()
	{
		// #2050: the desktop sticky rail holding the primary sign-up CTA was the
		// last child of its two-column grid (put there for #1755's layout), even
		// though CSS placed it visually in the right-hand column - so a keyboard
		// user had to tab through the report button, the map, its Leaflet
		// attribution links and the organization's contact links (7 stops) before
		// ever reaching the CTA. Fixed by making the <aside> the *first* child of
		// the grid and pinning both children back to their original visual
		// column/row with explicit grid placement (lg:col-start-*/lg:row-start-*),
		// decoupling DOM/focus order from visual order. Axe has no rule for a
		// DOM-order-vs-visual-order mismatch (WCAG 2.4.3 Focus Order is exactly
		// the kind of thing axe-core documents as untestable), so this asserts
		// the fact directly the same way HomePage_SkipLink_MovesFocusToMainContent
		// above does for the skip link.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new
		{
			name = $"A11y Focus Order Org {suffix}",
			contactEmail = "contact@example.org",
			contactPhone = "+49 30 1234567",
			website = "https://example.org",
		});
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"A11y Focus Order Test {suffix}",
			descriptionDe = "Seeded for #2050 focus-order coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var start = DateTimeOffset.UtcNow.AddDays(5);
		(await http.PostAsJsonAsync($"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
		{
			startDateTime = start,
			endDateTime = start.AddHours(2),
			maxParticipants = 5,
			recurrenceCount = 1,
		})).EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Proves the states this regression depends on actually rendered: the
		// report button (Vera is signed in and not the owner) and the sign-up CTA.
		await Expect(Page.GetByTestId("report-opportunity")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var ctaButton = Page.GetByTestId("signup-cta").GetByRole(AriaRole.Button);
		await Expect(ctaButton).ToBeVisibleAsync();

		// Same skip-link jump as HomePage_SkipLink_MovesFocusToMainContent, so
		// the count below starts from <main> rather than depending on how many
		// links the Header itself happens to have.
		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();

		// First stop inside <main> is the organization link in the page's
		// eyebrow; the second must be the sign-up CTA now that the rail is the
		// grid's first child - not the report button, which sits later in the
		// reading column.
		await Page.Keyboard.PressAsync("Tab");
		await Page.Keyboard.PressAsync("Tab");
		await Expect(ctaButton).ToBeFocusedAsync();
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_MapMarker_HasAccessibleName()
	{
		// No seeded opportunity ever gets real coordinates - VisualTests always
		// runs against FakeGeocodingService, which reports TransientFailure - so
		// the map is structurally unreachable by every other scan in this file.
		// Patch coordinates the way SingleMarkerMapTouchScrollTests does to
		// exercise it, and assert the marker's accessible name directly rather
		// than relying only on the axe scan (an unnamed role="button" tab stop
		// is WCAG 4.1.2 / axe's button-name rule).
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"Marker A11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var title = $"Marker A11y Test {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Seeded for the map marker accessible-name regression (#1681).",
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

		var marker = Page.Locator(".leaflet-marker-icon");
		await Expect(marker).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(marker).ToHaveAttributeAsync("role", "button");
		await Expect(marker).ToHaveAttributeAsync("title", "Teststrasse 1, 12345 Musterstadt");

		// The map container itself (not just the marker) needs an accessible
		// name too (#2058) - it stays a focusable, swipeable target even with
		// every pan/zoom interaction disabled, so an unnamed container would
		// be as much of a silent tab stop as the marker was before #1681.
		// role="group", not "img": "img" flattens focusable descendants like
		// the marker above out of the accessibility tree entirely.
		var mapContainer = Page.Locator(".leaflet-container");
		await Expect(mapContainer).ToHaveAttributeAsync("role", "group");
		await Expect(mapContainer).ToHaveAttributeAsync(
			"aria-label", "Map showing the location of Teststrasse 1, 12345 Musterstadt");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileOverviewPage_HasNoSeriousA11yViolations()
	{
		// /profile renders Profile Details and Badges only. Invitations and
		// sign-ups live at /my-signups; notifications, export and deletion at
		// /profile/settings - both scanned separately below.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MyEngagementsPage_HasNoSeriousA11yViolations()
	{
		// Invitations/sign-ups live on their own page rather than under
		// /profile, so the profile scan above does not reach them - this is
		// their only page-level scan.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Pinned to the page's <h1>: the header band's title and the sr-only
		// section heading further down both carry this name, so an unqualified
		// lookup matches two elements.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// Olaf's seed data always organizes at least one org, so FastSignInAsync
	// always resolves a pinned id for him to navigate straight to.
	private async Task NavigateToOrgAppDashboardAsOlafAsync(Uri frontend)
	{
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AsOlaf_SkipLink_MovesFocusToMainContent()
	{
		// Same bypass gap as HomePage's skip link, but the
		// org app shell's header (org switcher, notification bell, avatar menu,
		// breadcrumb + quick actions) is a separate implementation from the
		// public site's - this covers OrgAppLayout's own copy.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Keyboard.PressAsync("Tab");
		var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content" });
		await Expect(skipLink).ToBeFocusedAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();
	}

	[Test]
	public async Task EngagementManagementPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Seeds a fresh org/opportunity/engagement rather than relying on olaf's
		// shared seed data, which would let this skip when no published
		// opportunity with a pending applicant happens to exist - leaving the
		// page's "Confirm" button with no guaranteed coverage.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"EngagementManagementA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"EngagementManagementA11y Opportunity {suffix}",
			descriptionDe = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "For the a11y scan." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// On a nested route, the h1 must track the breadcrumb's trailing
		// "extra" segment (the opportunity title, set via
		// useSetOrgBreadcrumbExtra) rather than staying on the parent tab's
		// own label ("Opportunities") - the one place this pageTitle logic
		// could regress silently.
		await Expect(Page.Locator("h1")).Not.ToHaveTextAsync("Opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task PrivacyPolicyPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	// One case per administration section: separate routes behind a shared left
	// rail, so a single scan of /administration covers only the first.
	//
	// [Retry(2)]: this LoginAsync call site lands in the first concurrent batch,
	// where the Keycloak round trip can outlast any fixed timeout while Aspire
	// is still warming up. Cheaper than inflating every caller's timeout.
	[Test]
	[Retry(2)]
	[Arguments("organizations")]
	[Arguments("users")]
	[Arguments("reports")]
	[Arguments("audit-log")]
	public async Task AdministrationPage_HasNoSeriousA11yViolations(string section)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/administration/{section}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OpportunitiesPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A scan of a page whose list failed to load passes vacuously.
		await Expect(Page.GetByTestId("opportunities-keyword-input"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
