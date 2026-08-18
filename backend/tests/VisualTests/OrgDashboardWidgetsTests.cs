using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the org dashboard's widget grid (Calendar, Upcoming
/// Opportunities, Settings, To-Do, Create opportunity, Volunteers), which
/// lets an organizer see pending-application and confirmed-volunteer counts
/// without navigating to another tab. The pending queue lives in the "Needs
/// your attention" tile and reads as resolved when empty; the confirmed
/// total sits in its own neutral "Volunteers" tile. Customization itself
/// (add/remove/place via the "Edit" quick action) is covered by
/// OrgDashboardCustomizeTests.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardWidgetsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Dashboard_ShowsEveryDefaultWidget_ForFreshOrganization()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual762 Widgets", pinnedOrgId!.Value);

		// Every widget in DEFAULT_LAYOUT renders on the dashboard tab itself -
		// no navigating to another tab needed.
		var todoWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Needs your attention" }),
		});
		var volunteersWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Volunteers", Exact = true }),
		});
		var upcomingWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Upcoming opportunities" }),
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
		await Expect(volunteersWidget).ToBeVisibleAsync();
		await Expect(upcomingWidget).ToBeVisibleAsync();
		await Expect(calendarWidget).ToBeVisibleAsync();
		await Expect(settingsWidget).ToBeVisibleAsync();

		// A brand-new organization has nothing waiting, so the attention
		// widget reads as resolved instead of putting "0 Pending
		// sign-ups" and a live call to action under an urgency headline -
		// see ToDoWidget_ReadsAsResolvedAndOffersNoCta_... below for the pair.
		await Expect(todoWidget.GetByTestId("todo-widget-resolved"))
			.ToHaveTextAsync("Nothing pending - every sign-up is handled.");
		await Expect(todoWidget.GetByTestId("todo-widget-stat-pending")).ToHaveCountAsync(0);

		// The neutral confirmed-volunteer total lives in its own plain stats tile,
		// so a queue to work through and a running total do not share one urgent
		// framing. The label reads "Confirmed", not "Signed-up", because that is
		// what the API counts - see VolunteerStatsWidget.tsx.
		await Expect(todoWidget).Not.ToContainTextAsync("Confirmed volunteers");
		await Expect(volunteersWidget).ToContainTextAsync("Confirmed volunteers");
		// Selects on data-testid rather than the text-3xl Tailwind class, which a
		// purely cosmetic restyle would silently make match nothing.
		await Expect(volunteersWidget.GetByTestId("volunteer-stats-stat-confirmed"))
			.ToHaveTextAsync("0");

		// No opportunities yet, so the Upcoming Opportunities widget shows its
		// empty state instead of a stale/placeholder list.
		await Expect(upcomingWidget.GetByText("No upcoming opportunities.")).ToBeVisibleAsync();

		// The calendar itself still renders (retains month/week/day views).
		await Expect(calendarWidget.Locator(".rbc-calendar")).ToBeVisibleAsync();

		// Settings widget surfaces the org identity and a link back to the
		// full Settings tab instead of duplicating the whole edit form.
		// Singular member count must use "1 member", not "1 members".
		// Assert on the member-count link's own text rather than the whole
		// widget's flattened text - Playwright concatenates sibling DOM text
		// with no separator, so the org name's random Guid suffix can glue
		// directly onto the leading "1" (e.g. "...dc97351 member") and defeat
		// a regex checked against the whole section.
		await Expect(settingsWidget.GetByRole(AriaRole.Link, new() { Name = "member" }))
			.ToHaveTextAsync("1 member");
		await Expect(settingsWidget.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }))
			.ToBeVisibleAsync();

		// Many flows across this suite (see AuthHelper.GoToOrgAppDashboardAsync
		// callers) expect this button on the dashboard. It lives inside the
		// "Create opportunity" widget tile, but the testid/click target is the
		// same one they all use.
		var createOpportunityWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Create opportunity" }),
		});
		await Expect(createOpportunityWidget).ToBeVisibleAsync();
		await Expect(createOpportunityWidget.GetByTestId("create-opportunity-btn"))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task ToDoWidget_ReadsAsResolvedAndOffersNoCta_UntilASignUpIsActuallyWaiting()
	{
		// The widget branched only on loading / error / kpis-present,
		// so an empty queue rendered "0 Pending sign-ups" under the "Needs
		// your attention" headline next to a live "View pending sign-ups"
		// link that led to a list with no rows - an urgency headline over a
		// zero, training the organizer to ignore the one tile meant to catch
		// their eye. Both directions are asserted here: nothing waiting reads
		// as resolved with no call to action, and a real pending sign-up
		// brings the count and the link back.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A dedicated fresh organization rather than olaf's shared seed org -
		// that org accumulates sign-ups across a full suite run, and this test
		// asserts on an exact pending count.
		var suffix = Guid.NewGuid().ToString("N");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"Visual1780 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var todoWidget = Page.GetByTestId("widget-tile-ToDo");
		await Expect(todoWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var pendingCta = todoWidget.GetByRole(
			AriaRole.Link, new() { Name = "View pending sign-ups" });

		await Expect(todoWidget.GetByTestId("todo-widget-resolved"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(pendingCta).ToHaveCountAsync(0);
		await Expect(todoWidget.GetByTestId("todo-widget-stat-pending")).ToHaveCountAsync(0);

		// Now give the queue something to hold: an open opportunity vera signs
		// up for, left unconfirmed so it stays Pending.
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"Pending Queue Opportunity {suffix}",
			descriptionDe = "Created by ToDoWidget_ReadsAsResolvedAndOffersNoCta_UntilASignUpIsActuallyWaiting",
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

		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");
		(await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Sign-up for the #1780 pending queue" }))
			.EnsureSuccessStatusCode();

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(todoWidget.GetByTestId("todo-widget-stat-pending"))
			.ToHaveTextAsync("1", new() { Timeout = 15_000 });
		// Exact match, so this also pins the singular label (i18n's
		// pendingEngagements_one) rather than passing on "Pending sign-ups".
		await Expect(todoWidget.GetByText("Pending sign-up", new() { Exact = true }))
			.ToBeVisibleAsync();
		await Expect(pendingCta).ToBeVisibleAsync();
		await Expect(todoWidget.GetByTestId("todo-widget-resolved")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task ToDoWidget_OffersNoCta_WhenTheDashboardCountsFailToLoad()
	{
		// The "View pending sign-ups" link must sit inside the kpis-present
		// branch: outside it, a failed fetch renders a live call to action beside
		// the error banner, offering to work a queue whose size the page just
		// failed to read. Both count tiles surface their own failure and nothing
		// else.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"Visual1780 Error {Guid.NewGuid():N}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		// Only the KPI endpoint fails - the layout endpoint below it
		// (.../dashboard/layout) is deliberately left alone by this glob, so
		// the widget grid itself still renders and the tiles can be asserted
		// on.
		await Page.RouteAsync($"**/v1/organizations/{organizationId}/dashboard", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var todoWidget = Page.GetByTestId("widget-tile-ToDo");
		await Expect(todoWidget.GetByText("Failed to load summary."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(todoWidget.GetByRole(AriaRole.Link, new() { Name = "View pending sign-ups" }))
			.ToHaveCountAsync(0);
		await Expect(todoWidget.GetByTestId("todo-widget-resolved")).ToHaveCountAsync(0);
		await Expect(todoWidget.GetByTestId("todo-widget-stat-pending")).ToHaveCountAsync(0);

		var volunteersWidget = Page.GetByTestId("widget-tile-VolunteerStats");
		await Expect(volunteersWidget.GetByText("Failed to load the volunteer count."))
			.ToBeVisibleAsync();
		await Expect(volunteersWidget.GetByTestId("volunteer-stats-stat-confirmed"))
			.ToHaveCountAsync(0);
	}

	[Test]
	public async Task Dashboard_HasNoOrgNameHeading_AndWidgetLinksReachEverySubpage()
	{
		// OrgAppShell's h1 renders the tab/page title ("Dashboard"), never the org
		// name - the header's org switcher already shows that, and duplicating it
		// per page is what this asserts against. Also asserts the dashboard's own
		// widgets are a second, independent way to reach every subsite, not just
		// the tab bar (covered in OrgAppMobileResponsiveTests).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual771 Reachability", pinnedOrgId!.Value);

		// OrgAppShell renders one page-title h1, but it must still not
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

		// Opportunities: reachable via a dashboard widget's "opportunities" link -
		// scoped to that widget, since the page header's section rail (added with
		// OrgPageHeader.tsx) carries its own "Opportunities" link now too and this
		// test is specifically about the widgets being an independent second way
		// to reach every subpage.
		await Page.GetByTestId("widget-tile-UpcomingOpportunities")
			.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
		await Page.WaitForURLAsync(
			$"{origin}/app/{organizationId}/dashboard/opportunities", new() { Timeout = 10_000 });

		await Page.GoBackAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard$"), new() { Timeout = 10_000 });

		// Members: reachable via the Settings widget's member-count link - scoped
		// to that widget since the tab bar has its own separate "Members" link.
		// The link reads "1 member" (singular) for a fresh single-member org, so
		// match on "member" rather than "members".
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
		// CalendarWidget imported the German date-fns locale into its
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
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

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
		// The `messages` object passed to react-big-calendar never
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
			titleDe = oppTitle,
			descriptionDe = "Created by CalendarWidget agenda-header i18n test",
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
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

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
	public async Task CalendarWidget_ToolbarDateRangeHeader_UsesSharedDateFormat_NotAmbiguousDdMmYyyy()
	{
		// The Agenda/Week toolbar's date-range label (e.g. "14/08/2026 -
		// 13/09/2026") came straight from react-big-calendar's own
		// agendaHeaderFormat/dayRangeHeaderFormat defaults (date-fns' locale
		// default 'P' token, or a year-less "MMMM dd" range for Week), never
		// routed through the site's shared formatDate/formatDateTime helpers
		// that every other date on the site - including this same widget's own
		// event chips - goes through. A raw DD/MM/YYYY pair is genuinely
		// ambiguous for a mixed EN/DE audience, and the Week range dropped the
		// year entirely. Both are now built from formatDate, so this asserts
		// the header always carries a spelled-out month and an explicit year
		// on both sides, joined with the site's plain ASCII " - " rather than
		// react-big-calendar's own Unicode en dash separator.
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
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Visual1959 {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		// Gives the widget a real event row alongside the toolbar header, same
		// as the reported repro (comparing the header against an event row a
		// few lines below it).
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"Visual1959 Opportunity {suffix}",
			descriptionDe = "Created by CalendarWidget toolbar date-format test",
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
		var calendarWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Calendar", Exact = true }),
		});
		await Expect(calendarWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var toolbarLabel = calendarWidget.Locator(".rbc-toolbar-label");
		var viewGroup = calendarWidget.Locator(".rbc-btn-group").Last;

		// "d MMM yyyy - d MMM yyyy" - the same shape formatDate (dateStyle:
		// medium) produces everywhere else on the site, on both sides of the
		// range regardless of whether the range crosses a month boundary.
		const string sharedDateFormatPattern = @"^\d{1,2} [A-Za-z]+ \d{4} - \d{1,2} [A-Za-z]+ \d{4}$";

		// The widget defaults to Month view (its own default for a "full"
		// placement, see defaultViewForSize) - captured before switching so
		// the waits below can confirm each click's re-render actually landed
		// rather than reading InnerTextAsync() in a race with React's update.
		var monthLabel = await toolbarLabel.InnerTextAsync();

		await viewGroup.GetByRole(AriaRole.Button, new() { Name = "Agenda", Exact = true }).ClickAsync();
		await Expect(toolbarLabel).Not.ToHaveTextAsync(monthLabel);
		var agendaLabel = await toolbarLabel.InnerTextAsync();
		Regex.IsMatch(agendaLabel, @"^\d{1,2}/\d{1,2}/\d{4}").Should().BeFalse(
			$"the Agenda toolbar header must not fall back to a raw, locale-ambiguous DD/MM/YYYY pair (got \"{agendaLabel}\")");
		agendaLabel.Should().NotContain("\u2013",
			"the shared date formatter joins with a plain ASCII hyphen, not react-big-calendar's own en dash default");
		agendaLabel.Should().MatchRegex(sharedDateFormatPattern,
			$"the Agenda toolbar header must use the site's shared date format (got \"{agendaLabel}\")");

		await viewGroup.GetByRole(AriaRole.Button, new() { Name = "Week", Exact = true }).ClickAsync();
		await Expect(toolbarLabel).Not.ToHaveTextAsync(agendaLabel);
		var weekLabel = await toolbarLabel.InnerTextAsync();
		weekLabel.Should().NotContain("\u2013",
			"the shared date formatter joins with a plain ASCII hyphen, not react-big-calendar's own en dash default");
		weekLabel.Should().MatchRegex(sharedDateFormatPattern,
			$"the Week toolbar header must carry an explicit year on both sides via the site's shared date format, "
				+ $"not react-big-calendar's own year-less \"MMMM dd\" range default (got \"{weekLabel}\")");
	}

	[Test]
	public async Task CalendarWidget_MobileViewport_ToolbarButtonsAndAgendaColumnStayReachable()
	{
		// WidgetCard only set overflow-y-auto on its content wrapper, and
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
			titleDe = oppTitle,
			descriptionDe = "Created by CalendarWidget mobile overflow test",
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

		// Narrow the browser to the mobile viewport this row overflows at.
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
		// CalEvents, and the Calendar's components/eventPropGetter/messages
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
			titleDe = oppTitle,
			descriptionDe = "Created by CalendarWidget color-save test",
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

	[Test]
	public async Task UpcomingOpportunitiesWidget_ListsAPublishedOpportunity_WithItsNextSlotTime()
	{
		// The suite only ever asserted this widget's *empty* state, so nothing
		// covered the one branch that touches the data: a populated row. A
		// change that called a Date method on nextTimeSlotStart shipped past
		// every frontend check because the generated API client types that
		// field as Date while handing callers the raw JSON string (it parses
		// with a plain JSON.parse and no reviver) - the widget threw and fell
		// into its error boundary for any organization that actually had an
		// upcoming opportunity. See lib/upcomingOpportunities.ts, whose unit
		// tests pin the same contract at the pure-function level.
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
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"Visual Upcoming {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"Visual Upcoming Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by the Upcoming Opportunities widget test",
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

		var upcomingWidget = Page.GetByTestId("widget-tile-UpcomingOpportunities");
		await Expect(upcomingWidget).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The row itself, its formatted slot time, and its capacity line - a
		// crash inside the widget would replace all three with the tile's
		// error-boundary fallback instead.
		await Expect(upcomingWidget.GetByRole(AriaRole.Link, new() { Name = oppTitle }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(upcomingWidget).ToContainTextAsync(start.ToString("yyyy"));
		await Expect(upcomingWidget).ToContainTextAsync("0/5 sign-ups");
		await Expect(upcomingWidget.GetByText("This widget couldn't be displayed"))
			.ToHaveCountAsync(0);
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

	/// <summary>
	/// Mints a token straight from Keycloak's password grant, for the
	/// second actor in a test (vera signing up while the browser stays
	/// logged in as olaf) - same helper as EngagementManagementFiltersTests.
	/// </summary>
	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}
}
