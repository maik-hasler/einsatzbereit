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
// #1316: HomeCta_ZeroOrgs_CreatingOrgEntersItsDashboardDirectly below needs
// vera to deterministically have zero organizations - opts the whole class
// into fixture.ResetAsync() and a keyed [NotInParallel] so only other
// classes sharing the "visualtests-db" key (not the whole assembly) are
// excluded while this one mutates her organization membership.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
public class OrgAppRestructureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task GlobalHeader_NeverShowsOrgSwitcher_OutsideAppShell()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// fixture.ResetAsync() guarantees vera organizes nothing at this
		// point, so the CTA is deterministically the "create" button, not
		// the dashboard overview link.
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" });
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page).ToHaveURLAsync($"{origin}/app");
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task LegacyOrganizationDashboardUrl_NoLongerRoutes_FallsThroughToNotFound()
	{
		// #844: the pre-restructure /organizations/{id}/dashboard redirect had
		// no in-app link pointing at it (only /app/{id}/dashboard is ever
		// linked) and was removed - a direct visit now falls through to the
		// catch-all NotFoundPage, same as the /app legacy entry above.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var homeOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, homeOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}/dashboard");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page).ToHaveURLAsync($"{origin}/organizations/{organizationId}/dashboard");
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}
}
