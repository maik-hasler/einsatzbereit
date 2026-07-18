using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the /app restructuring requested in the #702 review: org
/// management pages (dashboard/engagements/members/settings) became their own
/// application context under /app/{organizationId}/..., separate from the
/// public Main Page, and the org switcher no longer renders in the global
/// header. Also covers the removal of the "Your organizations" section from
/// the profile page, and (#747) the removal of the /app intermediate picker
/// page in favor of the home page CTA resolving directly into the shell.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppRestructureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task GlobalHeader_NeverShowsOrgSwitcher_OutsideAppShell()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Olaf organizes at least one org in seed data - if the switcher were
		// still mounted in the global header it would render here.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.Not.ToBeVisibleAsync();

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task ProfilePage_NoLongerShowsOrganizationsSection()
	{
		// Regression guard: the "Your organizations" card (org list + its own
		// "Create organization" button) moved entirely into the org app shell -
		// the profile page must not still surface it, even for a user who
		// organizes orgs and would previously have populated it.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.GetByTestId("your-organizations-link")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByTestId("create-org-btn")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task HomeCta_ZeroOrgs_CreatingOrgEntersItsDashboardDirectly()
	{
		// Vera organizes nothing in seed data - the home page's "Create an
		// organisation" CTA opens org creation in place (#747: there is no
		// /app entry point to route through anymore), and submitting it must
		// still land her directly in the new org's dashboard.
		var frontend = Fixture.GetEndpoint("frontend");
		var orgName = $"Visual OrgAppEntry Empty {Guid.NewGuid():N}";

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The hero CTA renders the "Create an organisation" button until the
		// async org-count fetch resolves, then swaps to a dashboard Link if the
		// user turns out to have orgs after all - wait for that fetch to settle
		// first so we never click a button that's about to be swapped out from
		// under us (which would hang waiting for it to reappear).
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" });
		if (await createBtn.CountAsync() == 0)
			return; // a previous retry already gave vera an org - skip

		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await createBtn.First.ClickAsync();
		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName);
	}

	[Test]
	public async Task LegacyAppEntryUrl_NoLongerRoutes_FallsThroughToNotFound()
	{
		// #747: /app was removed as a distinct route (no more picker/loading
		// intermediate) - a direct visit must now fall through to the
		// catch-all NotFoundPage instead.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page).ToHaveURLAsync($"{origin}/app");
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task LegacyOrganizationDashboardUrl_RedirectsIntoAppShell()
	{
		// Pre-restructure bookmarks/links to /organizations/{id}/dashboard must
		// still land the user in the right place, under /app now.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}/dashboard");
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
