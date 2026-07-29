using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccessibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// #973: axe reports the "page has no h1" defect (page-has-heading-one) and
	// most landmark-structure defects at "moderate" impact, not "serious" or
	// "critical" - the plain serious/critical filter below let all four org app
	// pages ship with no h1 for months without CI ever seeing it. Escalate just
	// these rule IDs to CI-blocking rather than every moderate violation, which
	// would also flag things like color-contrast-enhanced noise unrelated to
	// this gate's purpose.
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
	public async Task OrganizationsDirectoryPage_HasNoSeriousA11yViolations()
	{
		// #763: the public organization directory page - not covered by any
		// existing a11y test, which is exactly how the text-gray-400 city-line
		// contrast violation (same defect as the "Received:" meta line fixed
		// alongside it) would have shipped unnoticed.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MyEngagementsPage_AsVera_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
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
	public async Task ProfileOverviewPage_HasNoSeriousA11yViolations()
	{
		// #794: /profile was consolidated from a Profile/Activity tab switcher
		// into a single page - Profile Details, Badges, and My Engagements all
		// render together here.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ProfileOverviewPage_EditMode_HasNoSeriousA11yViolations()
	{
		// #794: Edit/Save/Cancel moved from inline buttons into the header's
		// quick actions - the read-only scan above never opens the edit form,
		// so scan it separately here. Also asserts the Badges/My Engagements
		// sections stay mounted and visible alongside the open edit form,
		// since they no longer live behind a separate tab.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Engagements" })).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationProfilePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());

		// Wait for org links from opportunity cards; skip gracefully if page load times out
		var orgLinks = Page.Locator("ul > li .relative.z-10 a");
		try
		{
			await orgLinks.First.WaitForAsync(new() { Timeout = 30_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("home page did not load in time");
		}

		Skip.When(await orgLinks.CountAsync() == 0, "no opportunities seeded");

		var href = await orgLinks.First.GetAttributeAsync("href");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UserProfilePage_HasNoSeriousA11yViolations()
	{
		// #576: the public /users/{userId} page previously showed only avatar,
		// name, engagement count, and badges - it now also renders bio/skills/
		// languages/preferredContact via the shared ProfileFieldsView component.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");
		Skip.When(userId is null, "could not resolve the logged-in user's id");

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UserAchievementsPage_HasNoSeriousA11yViolations()
	{
		// #800: /users/{userId}/achievements was never visited by any a11y
		// test, despite being a major user-facing page (BadgeGrid's badge
		// cards + hover/focus tooltips).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");
		Skip.When(userId is null, "could not resolve the logged-in user's id");

		await Page.GotoAsync($"{origin}/users/{userId}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

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

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_CalendarWidgetColorDialog_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #762 rebuilt the dashboard as a widget grid; the Calendar widget's
		// color-picker dialog only exists in the DOM while open, so the plain
		// page-load scan above can't reach it.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var calendarEvent = Page.Locator(".rbc-event").First;
		try
		{
			await calendarEvent.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("olaf's org has no calendar events seeded for the current month");
		}

		await calendarEvent.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgOpportunitiesPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// #771: the tab bar is gone - reach the page via the dashboard's own
		// widget links instead.
		await Page.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Opportunities");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgMembersPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// The tab bar is gone (dashboard UX redesign) - reach Members via the
		// Settings widget's member-count link instead (its accessible name is
		// "N member(s)" - #834 made the count grammatically correct German/
		// English plural forms, so match "member" to cover both N=1 and N>1).
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Members");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// #771: the tab bar is gone - reach the page via the Settings widget's
		// "Edit settings" link instead.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: OrgAppShell previously rendered no h1 on any org app page.
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Settings");

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditMode_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The Edit/Save/Cancel buttons moved from inline page content into the
		// header's quick actions (#771 follow-up) - the read-only scan above
		// never opens the edit form itself, so scan it separately here.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditModeValidationError_AsOlaf_HasNoSeriousA11yViolations()
	{
		// #851: OrgSettingsPage gained the same react-hook-form + zod
		// validation as CreateOrganizationModal (previously it had none) -
		// scan the new inline validation-error state, not just the clean
		// edit-mode form covered above.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		await Page.Locator("#org-name").FillAsync("");
		await Page.GetByTestId("quick-action-save").ClickAsync();

		await Expect(Page.Locator("#org-name-error")).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_EditModeWithLogo_HasNoSeriousA11yViolations()
	{
		// #845: the "Remove" button next to the logo only renders once an
		// organization has a logo. Olaf's seeded org has none, so the edit-mode
		// scan above never renders it - seed a fresh org with a logo here
		// instead of mutating Olaf's shared seed data.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await GetAccessTokenAsync();

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"A11yLogo {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		using var content = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(fileContent, "file", "logo.png");

		(await http.PutAsync($"/v1/organizations/{organizationId}/logo", content)).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	private async Task<string> GetAccessTokenAsync()
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in localStorage after login");
		return token!;
	}

	// 1x1 transparent PNG.
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Test]
	public async Task OrgDashboardPage_EditMode_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The customizable widget grid's edit-mode chrome (inert widget
		// content, move/resize/remove toolbar) only exists in the DOM while
		// editing - the read-only scan above can't reach it. The "Add
		// Widget" modal and the corner-to-corner placement surface are their
		// own DOM-only-while-open states, scanned separately below.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_PlacingAWidget_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Corner-to-corner placement (#782) renders its own extra surface
		// while active - the green/blue/red-tinted grid backdrop and the
		// role="status" placement banner - that the plain edit-mode scan
		// above never reaches, since it never clicks "Move or resize".
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_AddWidgetModal_AsOlaf_HasNoSeriousA11yViolations()
	{
		// The "Add Widget" picker (#771 follow-up review feedback) only
		// exists in the DOM while open - the edit-mode scan above never
		// opens it.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrgDashboardPage_QuickCheckInAndSettingsIconWidgets_AsOlaf_HasNoSeriousA11yViolations()
	{
		// QuickCheckIn and SettingsIcon aren't in DEFAULT_LAYOUT (see
		// widgetCatalog.ts), so a fresh org's dashboard scan above never
		// renders them for real - only AddWidgetModal's static mockup preview
		// gets scanned incidentally as part of that dialog. Add both here so
		// their actual rendered content (the opportunity <select> + scan
		// button, and the settings shortcut tile) gets its own axe pass.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByTestId("add-widget-option-QuickCheckIn").ClickAsync();
		await dialog.GetByTestId("add-widget-option-SettingsIcon").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Expect(Page.GetByTestId("widget-tile-QuickCheckIn")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("widget-tile-SettingsIcon")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task EngagementManagementPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		// Engagement management is nested in the org app (#751) - reachable
		// from the Opportunities page's "Manage applications" link, not from
		// the public opportunity detail page anymore.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		// #771: the tab bar is gone - reach Opportunities via a dashboard widget link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var manageLink = Page.GetByRole(AriaRole.Link, new() { Name = "Manage applications" });
		try
		{
			await manageLink.First.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("olaf has no published opportunity with the manage-applications action");
		}

		await manageLink.First.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #973: on a nested route, the h1 must track the breadcrumb's trailing
		// "extra" segment (the opportunity title, set via
		// useSetOrgBreadcrumbExtra) rather than staying on the parent tab's
		// own label ("Opportunities") - the one place this pageTitle logic
		// could regress silently.
		await Expect(Page.Locator("h1")).Not.ToHaveTextAsync("Opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationsPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

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

	[Test]
	public async Task ImprintPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ContactPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/contact");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task NotFoundPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/this-page-does-not-exist");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task SignUpModal_OpenTimeSlotDropdown_HasNoSeriousA11yViolations()
	{
		// #573: the native time slot <select> was replaced with a custom
		// accessible combobox/listbox - assert the open dropdown itself is
		// axe-clean, not just the page around it.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");

		// "Select a slot" only exists on an opportunity's own detail page
		// (VolunteerOpportunityDetailPage.tsx) - the home page's cards link
		// there but never render the button themselves. Filter to Waitlist-type
		// opportunities (seed data has two, both with open capacity) and follow
		// the first card's link in, matching the navigation pattern
		// VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations above uses.
		await Page.GotoAsync($"{frontend}?participationType=Waitlist");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no waitlist opportunity seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		try
		{
			await signUpBtn.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no waitlist opportunity with open slots seeded");
		}

		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var dropdown = Page.Locator("#sign-up-time-slot");
		Skip.When(await dropdown.CountAsync() == 0, "opportunity has no time slots to pick from");

		await dropdown.ClickAsync();
		await Expect(Page.Locator("[role='option']").First).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task CreateVolunteerOpportunityModal_HasNoSeriousA11yViolations()
	{
		// #676 Pitch 2 rewrote this modal with custom ARIA machinery (a manual
		// Tab trap, an aria-live step announcer, sr-only radio-cards, and a
		// nested unsaved-changes ConfirmDialog) that a plain page-load axe
		// scan can't reach, since the modal only exists in the DOM while open.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		// Dirty the form, then open the nested discard-changes confirmation -
		// both dialogs stacked must remain axe-clean together.
		await Page.Locator("#opportunity-title").FillAsync("A11y Axe Test");
		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes" }))
			.ToBeVisibleAsync();

		var nestedResult = await Page.RunAxe();
		AssertNoViolations(nestedResult);
	}

	[Test]
	public async Task CreateOrganizationModal_HasNoSeriousA11yViolations()
	{
		// #851: this modal gained react-hook-form + zod validation (it
		// previously had none) - scan both the clean state and the
		// blank-submit validation-error state it can now render.
		var frontend = Fixture.GetEndpoint("frontend");
		await NavigateToOrgAppDashboardAsOlafAsync(frontend);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);

		await createDialog.GetByTestId("modal-submit").ClickAsync();
		await Expect(createDialog.Locator("#create-org-name-error")).ToBeVisibleAsync(
			new() { Timeout = 5_000 });

		var errorResult = await Page.RunAxe();
		AssertNoViolations(errorResult);

		await createDialog.GetByTestId("modal-cancel").ClickAsync();
	}

	[Test]
	public async Task NotificationDropdown_Open_HasNoSeriousA11yViolations()
	{
		// #1384: the dropdown gained cursor-based "load more" pagination on top
		// of the existing mark-all-read/per-item controls - the panel only
		// exists in the DOM while open, so a plain page-load scan never reaches
		// it (see NotificationBell_OpensPanel_WhenClicked in NotificationTests.cs
		// for the equivalent functional-only coverage).
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"NotifA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"NotifA11y Opportunity {suffix}",
			description = "Created by AccessibilityTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
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

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(panel.Locator("li").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task AdministrationPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/administration");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
