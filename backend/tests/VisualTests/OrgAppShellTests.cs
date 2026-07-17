using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Live-verification coverage for #691/#702: the organizer dashboard moved
/// from /organizations/:organizationId/dashboard (tab state in a ?tab= query
/// param, reached via an OrganizationSwitcher dropdown that lived in the
/// global Header) to a dedicated app shell at /app/:organizationId/:tab (tab
/// is now a real route segment), reached only via the "Your organizations"
/// list on /profile.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppShellTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgSwitcher_IsGoneFromHeader_ProfileEntersAppShell_TabsAreRealLinks()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Regression guard: the org switcher must no longer be reachable from
		// the global Header on the Home page - it moved entirely into
		// OrgAppLayout (#691/#702).
		await Page.GotoAsync($"{origin}/");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var headerSwitcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		(await headerSwitcherBtn.CountAsync()).Should().Be(0,
			"the org switcher must not be reachable from the global Header any more");

		// /profile's "Your organizations" list is now the only in-app entry
		// point into the organizer app shell.
		if (!await GoToFirstOrganizationDashboardAsync())
			return; // olaf organizes no orgs in this run's seed data - skip

		await Expect(Page).ToHaveURLAsync(new Regex(@"/app/[^/]+/dashboard$"), new() { Timeout = 10_000 });

		// Tab navigation inside /app/:organizationId/* is real routed
		// <NavLink> navigation now, not buttons mutating a ?tab= query param.
		foreach (var label in new[] { "Calendar", "Engagements", "Members", "Settings" })
		{
			await Expect(Page.GetByRole(AriaRole.Link, new() { Name = label, Exact = true }))
				.ToBeVisibleAsync();
		}

		await Page.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }).ClickAsync();
		await Expect(Page).ToHaveURLAsync(new Regex(@"/members$"), new() { Timeout = 10_000 });
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}
}
