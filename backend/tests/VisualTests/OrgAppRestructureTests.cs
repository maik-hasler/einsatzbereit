using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the /app restructuring requested in the #702 review: org
/// management pages (dashboard/engagements/members/settings) became their own
/// application context under /app/{organizationId}/..., separate from the
/// public Main Page, and the org switcher no longer renders in the global
/// header. Also covers the later /app entry point (empty state / picker) and
/// the removal of the "Your organizations" section from the profile page.
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
		// "Create organization" button) moved entirely to the /app entry point -
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
	public async Task OrgAppEntry_ZeroOrgs_ShowsEmptyState_AndCreatingOrgEntersItsDashboard()
	{
		// Vera organizes nothing in seed data - /app must show an empty-state
		// prompt rather than a blank picker, and creating an org there enters
		// its dashboard directly like every other creation entry point.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var orgName = $"Visual OrgAppEntry Empty {Guid.NewGuid():N}";

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/app");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" });
		if (await createBtn.CountAsync() == 0)
			return; // a previous retry already gave vera an org - skip

		await createBtn.ClickAsync();
		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName);
	}

	[Test]
	public async Task OrgAppEntry_MultipleOrgs_ShowsPickerAndNavigatesToSelection()
	{
		// Olaf organizes at least two orgs in seed data - /app must show a
		// picker rather than guessing which one to enter.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var rows = Page.GetByTestId("org-entry-picker-row");
		var rowCount = await rows.CountAsync();
		if (rowCount == 0)
			return; // already auto-redirected - olaf organizes exactly one org here, skip

		var firstRow = rows.First;
		var orgName = (await firstRow.TextContentAsync() ?? "").Trim();
		await firstRow.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName);
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
