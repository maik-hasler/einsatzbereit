using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the /app restructuring requested in the #702 review: org
/// management pages (dashboard/engagements/members/settings) became their own
/// application context under /app/{org-slug}/..., separate from the public
/// Main Page, and the org switcher no longer renders in the global header.
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
	public async Task ProfilePage_YourOrganizationsLink_EntersAppShellOnThatOrgsDashboard()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.GetByTestId("your-organizations-link");
		if (await orgLink.CountAsync() == 0)
			return; // no org in seed - skip

		var orgName = (await orgLink.First.InnerTextAsync()).Trim();
		await orgLink.First.ClickAsync();

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
		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgLink = Page.GetByTestId("your-organizations-link");
		if (await orgLink.CountAsync() == 0)
			return; // no org in seed - skip

		var href = await orgLink.First.GetAttributeAsync("href");
		href.Should().NotBeNull();
		var organizationId = href!.Split('/')[2];

		await Page.GotoAsync($"{origin}/organizations/{organizationId}/dashboard");
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
