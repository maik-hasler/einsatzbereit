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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual762 Widgets");

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
		await Expect(todoWidget).ToContainTextAsync("Pending Applications");
		await Expect(todoWidget).ToContainTextAsync("Signed-up Volunteers");
		var statValues = todoWidget.Locator("p.text-3xl");
		await Expect(statValues.Nth(0)).ToHaveTextAsync("0");
		await Expect(statValues.Nth(1)).ToHaveTextAsync("0");

		// No opportunities yet, so the Upcoming Opportunities widget shows its
		// empty state instead of a stale/placeholder list.
		await Expect(upcomingWidget.GetByText("No upcoming opportunities.")).ToBeVisibleAsync();

		// The calendar itself still renders (retains month/week/day views).
		await Expect(calendarWidget.Locator(".rbc-calendar")).ToBeVisibleAsync();

		// Settings widget surfaces the org identity and a link back to the
		// full Settings tab instead of duplicating the whole edit form.
		await Expect(settingsWidget).ToContainTextAsync("1 members");
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
		// see OrgAppMobileResponsiveTests for tab-bar coverage. This test
		// keeps proving the h1 removal and that the dashboard's own widgets
		// are a second, independent way to reach every subsite (not just the
		// tab bar).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual771 Reachability");

		(await Page.Locator("h1").CountAsync())
			.Should().Be(0, "the dashboard no longer duplicates the org name as a heading");

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		var settingsWidget = Page.Locator("section", new()
		{
			Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Organization", Exact = true }),
		});

		// Opportunities: reachable via a dashboard widget's "opportunities" link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "opportunities" }).First.ClickAsync();
		await Page.WaitForURLAsync(
			$"{origin}/app/{organizationId}/opportunities", new() { Timeout = 10_000 });

		await Page.GoBackAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 10_000 });

		// Members: reachable via the Settings widget's member-count link -
		// scoped to that widget since the tab bar has its own, separate
		// "Members" link now too.
		await settingsWidget.GetByRole(AriaRole.Link, new() { Name = "members" }).ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/members", new() { Timeout = 10_000 });

		await Page.GoBackAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 10_000 });

		// Settings: reachable via the Settings widget's "Edit settings" link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/settings", new() { Timeout = 10_000 });
	}

	private async Task CreateOrganizationAsync(string namePrefix)
	{
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one) - and guarantees a clean, empty org
		// (no opportunities/engagements yet) for deterministic widget assertions.
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
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
