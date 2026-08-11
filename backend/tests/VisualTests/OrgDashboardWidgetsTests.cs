using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #762: the org dashboard tab was rebuilt from a bare
/// calendar into a widget grid (Calendar, Upcoming Opportunities, Settings,
/// To-Do) so an organizer sees pending-application and signed-up-volunteer
/// counts without navigating to another tab. #771 review feedback made the
/// grid customizable (add/remove/place via the "Edit" quick action) and
/// added a "Create Opportunity" widget to the default layout - see
/// OrgDashboardCustomizeTests for coverage of the customization itself.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardWidgetsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Dashboard_ShowsAllFourWidgets_ForFreshOrganization()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual762 Widgets", pinnedOrgId!.Value);

		// All four fixed widgets render on the dashboard tab itself - no
		// navigating to another tab needed.
		var todoWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Needs Your Attention" }),
		});
		var upcomingWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Upcoming Opportunities" }),
		});
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		var settingsWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Organization", Exact = true }),
		});

		await Expect(todoWidget).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(upcomingWidget).ToBeVisibleAsync();
		await Expect(calendarWidget).ToBeVisibleAsync();
		await Expect(settingsWidget).ToBeVisibleAsync();

		// A brand-new organization has no applications and no confirmed
		// volunteers yet - both KPI stats read 0.
		await Expect(todoWidget).ToContainTextAsync("Pending Sign-ups");
		await Expect(todoWidget).ToContainTextAsync("Signed-up Volunteers");
		// Selects on data-testid rather than the text-3xl Tailwind utility
		// class - see #1328, a purely cosmetic restyle of that class would
		// otherwise silently make these locators match nothing.
		await Expect(todoWidget.GetByTestId("todo-widget-stat-pending")).ToHaveTextAsync("0");
		await Expect(todoWidget.GetByTestId("todo-widget-stat-confirmed")).ToHaveTextAsync("0");

		// No opportunities yet, so the Upcoming Opportunities widget shows its
		// empty state instead of a stale/placeholder list.
		await Expect(upcomingWidget.GetByText("No upcoming opportunities.")).ToBeVisibleAsync();

		// The calendar itself still renders (retains month/week/day views).
		await Expect(calendarWidget.Locator(".rbc-calendar")).ToBeVisibleAsync();

		// Settings widget surfaces the org identity and a link back to the
		// full Settings tab instead of duplicating the whole edit form.
		// #834: singular member count must use "1 member", not "1 members".
		// Assert on the member-count link's own text rather than the whole
		// widget's flattened text - Playwright concatenates sibling DOM text
		// with no separator, so the org name's random Guid suffix can glue
		// directly onto the leading "1" (e.g. "...dc97351 member") and defeat
		// a regex checked against the whole section.
		await Expect(settingsWidget.GetByRole(AriaRole.Link, new() { Name = "member" }))
			.ToHaveTextAsync("1 member");
		await Expect(settingsWidget.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }))
			.ToBeVisibleAsync();

		// Regression guard: many existing Playwright flows across this suite
		// (see AuthHelper.GoToOrgAppDashboardAsync callers) expect this button
		// on the dashboard - #771 review feedback moved it from a bare button
		// above the grid into its own "Create Opportunity" widget tile (part
		// of the default layout), but the testid/click target is unchanged.
		var createOpportunityWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Create Opportunity" }),
		});
		await Expect(createOpportunityWidget).ToBeVisibleAsync();
		await Expect(createOpportunityWidget.GetByTestId("create-opportunity-btn"))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task Dashboard_HasNoOrgNameHeading_AndWidgetLinksReachEverySubpage()
	{
		// #771: the repo owner asked to remove the per-page org-name h1
		// entirely - the org switcher in the header already shows the org
		// name. #775/#777 brought the tab bar back (mobile burger submenu
		// needed it too), so that part of #771's removal no longer holds -
		// see OrgAppMobileResponsiveTests for tab-bar coverage. #973 then
		// gave OrgAppShell a real h1 again (axe's page-has-heading-one gate
		// had a blind spot that let every org app page ship with no h1 at
		// all) - but it renders the tab/page title ("Dashboard"), not the
		// org name, so #771's actual intent (no org-name duplication) still
		// holds. This test now asserts exactly that single, correctly-titled
		// h1, plus that the dashboard's own widgets are a second, independent
		// way to reach every subsite (not just the tab bar).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual771 Reachability", pinnedOrgId!.Value);

		// OrgAppShell renders one page-title h1 (#973), but it must still not
		// duplicate the org name the header's org switcher already shows.
		await Expect(Page.Locator("h1")).ToHaveCountAsync(1);
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Dashboard");

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		var settingsWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Organization", Exact = true }),
		});

		// Opportunities: reachable via a dashboard widget's "opportunities" link.
		await Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
		await Page.WaitForURLAsync(
			$"{origin}/app/{organizationId}/dashboard/opportunities", new() { Timeout = 10_000 });

		await Page.GoBackAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard$"), new() { Timeout = 10_000 });

		// Members: reachable via the Settings widget's member-count link -
		// scoped to that widget since the tab bar has its own, separate
		// "Members" link now too. #834: the link reads "1 member" (singular)
		// for a fresh single-member org, so match on "member" rather than
		// "members".
		await settingsWidget.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard/members", new() { Timeout = 10_000 });

		await Page.GoBackAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard$"), new() { Timeout = 10_000 });

		// Settings: reachable via the Settings widget's "Edit settings" link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard/settings", new() { Timeout = 10_000 });
	}

	[Test]
	public async Task CalendarWidget_MonthView_RendersGermanWeekdayLabels_WhenAppLocaleIsGerman()
	{
		// #878: CalendarWidget imported the German date-fns locale into its
		// `locales` map but never passed a `culture` prop to the underlying
		// Calendar component, so react-big-calendar always fell back to its
		// default (English) formatting regardless of the app's selected
		// language. German weekday abbreviations carry a trailing "."
		// (e.g. "Di.") that English abbreviations never do, so their
		// presence in the month header row is a signal that the culture is
		// actually applied - and one that holds on any date the test happens
		// to run on, unlike asserting a specific localized month name.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		// Switch to German via the header's language selector rather than
		// pre-seeding localStorage's i18nextLng before sign-in: aria-labels
		// are translated too (e.g. AccountControls' "User menu" button
		// becomes "Benutzermenü"), so setting the locale that early makes
		// FastSignInAsync's own "User menu" wait time out.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync();
		await Expect(calendarWidget.Locator(".rbc-header", new() { HasText = "Di." }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task CalendarWidget_AgendaView_RendersGermanColumnHeaders_WhenAppLocaleIsGerman()
	{
		// #1254: the `messages` object passed to react-big-calendar never
		// overrode `date`/`time`/`event` (among others), so its own English
		// defaults rendered these Agenda column headers regardless of the
		// app's selected language - a German organizer's first look at the
		// widget (Agenda is the default view on a narrow placement) showed
		// "Date | Time | Event".
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

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

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		// A dedicated fresh organization rather than olaf's shared pinned
		// org - that org accumulates widget-layout customization and dozens
		// of opportunities/notifications across the whole test suite over a
		// full run, and this test only cares about a lone Calendar widget in
		// its default (compact) placement.
		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Visual1254 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		// Gives the Calendar widget an event to render - without one, the
		// Agenda view shows its empty-state span instead of the table whose
		// column headers this test needs to inspect.
		var oppTitle = $"Visual1254 Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CalendarWidget agenda-header i18n test",
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

		var start = DateTimeOffset.UtcNow.AddDays(3);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		// Locate by the widget's stable heading id (WidgetCard's titleId), not
		// by heading text - the heading itself is what this test switches to
		// German below, so matching on "Calendar" would stop resolving the
		// moment the switch takes effect.
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.Locator("#widget-calendar-title"),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Deutsch" }).ClickAsync();

		var viewGroup = calendarWidget.Locator(".rbc-btn-group").Last;
		var agendaButton = viewGroup.GetByRole(AriaRole.Button, new() { Name = "Agenda", Exact = true });
		await agendaButton.ScrollIntoViewIfNeededAsync();
		await agendaButton.ClickAsync();

		var headerRow = calendarWidget.Locator(".rbc-agenda-table thead tr");
		await Expect(headerRow.GetByText("Datum", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(headerRow.GetByText("Uhrzeit", new() { Exact = true })).ToBeVisibleAsync();
		await Expect(headerRow.GetByText("Termin", new() { Exact = true })).ToBeVisibleAsync();
		// The pre-fix English defaults must not leak through alongside them.
		await Expect(headerRow.GetByText("Date", new() { Exact = true })).Not.ToBeVisibleAsync();
		await Expect(headerRow.GetByText("Time", new() { Exact = true })).Not.ToBeVisibleAsync();
		await Expect(headerRow.GetByText("Event", new() { Exact = true })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task CalendarWidget_MobileViewport_ToolbarButtonsAndAgendaColumnStayReachable()
	{
		// #812: WidgetCard only set overflow-y-auto on its content wrapper, and
		// html sets overflow-x: clip page-wide (global.css) - together, any
		// widget content wider than its rendered width (the Calendar widget's
		// toolbar button rows, and its Agenda table's fixed-width date/time
		// columns squeezing the flexible EVENT column) silently blew out past
		// the widget on a narrow viewport with no way to reach it, rather than
		// scrolling within the widget itself. Fixed by giving WidgetCard's
		// wrapper overflow-x-auto too (containing the blowout so it scrolls
		// instead of clipping) and letting the toolbar's button rows and the
		// Agenda table scroll horizontally on their own (global.css) instead
		// of being clipped or squeezed unreadably.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// Sign in and land on the dashboard at the default (desktop) viewport
		// first - FastSignInAsync's "User menu" button doesn't exist in the DOM
		// at all below the mobile breakpoint (see OrgAppMobileResponsiveTests),
		// only appearing inside the hamburger menu once opened.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

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

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Visual812 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		// Gives the Calendar widget an event to render - without one, the
		// Agenda view shows its empty-state span instead of the table whose
		// EVENT column this test needs to measure.
		var oppTitle = $"Visual812 Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CalendarWidget mobile overflow test",
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

		// Pinned to a fixed 10:00 UTC start rather than DateTimeOffset.UtcNow -
		// a "now + 3 days" slot inherits whatever time of day the suite happens
		// to run at, and a 2-hour slot starting late enough in the day crosses
		// midnight. The Agenda view then renders the one event as two rows (one
		// per day it touches), and the GetByText(oppTitle) lookup below hits a
		// Playwright strict-mode violation from matching both.
		var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(10), TimeSpan.Zero);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Narrow the browser to exactly the mobile viewport #812 was reported on.
		await Page.SetViewportSizeAsync(390, 844);

		var viewGroup = calendarWidget.Locator(".rbc-btn-group").Last;
		var agendaButton = viewGroup.GetByRole(AriaRole.Button, new() { Name = "Agenda", Exact = true });
		// Reachable only by scrolling this one row (its own overflow-x: auto,
		// not the whole page) - if the fix regresses back to a plain clipped
		// row, this button has no scrollable ancestor to bring it into view
		// and the click below times out instead of landing.
		await agendaButton.ScrollIntoViewIfNeededAsync();
		await Expect(agendaButton).ToBeVisibleAsync();
		await agendaButton.ClickAsync();

		var eventHeader = calendarWidget.Locator(".rbc-agenda-table thead th", new() { HasText = "Event" });
		await Expect(eventHeader).ToBeVisibleAsync(new() { Timeout = 10_000 });
		// Single EvaluateAsync per poll (not a bare BoundingBoxAsync read) so a
		// late layout pass on the freshly-rendered Agenda table can't be
		// sampled mid-reflow.
		var eventHeaderWidth = 0d;
		await PollUntilAsync(async () =>
		{
			eventHeaderWidth = await eventHeader.EvaluateAsync<double>(
				"el => el.getBoundingClientRect().width");
			return eventHeaderWidth > 80;
		}, () => "the EVENT column should stay legibly wide (the Agenda table scrolls "
			+ "horizontally instead) rather than being squeezed down to a couple "
			+ $"of characters to fit the narrow viewport (last observed width: {eventHeaderWidth}px)");
		await Expect(calendarWidget.GetByText(oppTitle)).ToBeVisibleAsync();

		var toolbarLabel = calendarWidget.Locator(".rbc-toolbar-label");
		var labelBeforeNext = await toolbarLabel.InnerTextAsync();

		var navGroup = calendarWidget.Locator(".rbc-btn-group").First;
		var nextButton = navGroup.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true });
		await nextButton.ScrollIntoViewIfNeededAsync();
		await Expect(nextButton).ToBeVisibleAsync();
		await nextButton.ClickAsync();
		await Expect(toolbarLabel).Not.ToHaveTextAsync(labelBeforeNext);

		var dayButton = viewGroup.GetByRole(AriaRole.Button, new() { Name = "Day", Exact = true });
		await dayButton.ScrollIntoViewIfNeededAsync();
		await Expect(dayButton).ToBeVisibleAsync();
		await dayButton.ClickAsync();
		await Expect(calendarWidget.Locator(".rbc-time-view")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CalendarWidget_SelectEventAndSaveColor_RecoloredEventSurvivesReload()
	{
		// #1397: calEvents, and the Calendar's components/eventPropGetter/messages
		// props, were rebuilt from scratch on every render (including on every
		// pointer movement while dragging the color picker below), fixed by
		// memoizing calEvents off calData and hoisting the static props out of
		// the component. This exercises the full select-event -> pick color ->
		// save round trip end to end, so a broken useMemo dependency array or a
		// debounced picker that never flushes its final value would show up as
		// a failing assertion here rather than only as a missed re-render.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

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

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Visual1397 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Visual1397 Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by CalendarWidget color-save test",
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

		// Close enough to "now" that it always falls in the current month, so
		// the widget's default month view shows it without switching tabs.
		var start = DateTimeOffset.UtcNow.AddHours(1);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var calendarEvent = calendarWidget.Locator(".rbc-event").First;
		await Expect(calendarEvent).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// No color set yet - eventPropGetter falls back to DEFAULT_EVENT_COLOR.
		await Expect(calendarEvent).ToHaveCSSAsync("background-color", "rgb(34, 105, 71)");

		await calendarEvent.ClickAsync();
		var colorDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(colorDialog).ToBeVisibleAsync();
		await Expect(colorDialog).ToContainTextAsync(oppTitle);

		var colorInput = Page.Locator("#event-color-picker");
		await Expect(colorInput).ToHaveValueAsync("#226947");

		const string newColor = "#3366cc";
		await colorInput.FillAsync(newColor);
		await Expect(Page.GetByText(newColor)).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
		await Expect(colorDialog).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Expect(calendarEvent).ToHaveCSSAsync("background-color", "rgb(51, 102, 204)");

		// Reload so calData (and the memoized calEvents derived from it) come
		// back fresh from the server, proving the color actually persisted
		// rather than only reflecting the modal's optimistic local update.
		await Page.ReloadAsync();
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var reloadedEvent = calendarWidget.Locator(".rbc-event").First;
		await Expect(reloadedEvent).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(reloadedEvent).ToHaveCSSAsync("background-color", "rgb(51, 102, 204)");
	}

	private async Task CreateOrganizationAsync(string namePrefix, Guid organizationId)
	{
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one) - and guarantees a clean, empty org
		// (no opportunities/engagements yet) for deterministic widget assertions.
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, organizationId);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
