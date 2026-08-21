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
	public async Task HomeCta_ZeroOrgs_CreatingOrgEntersItsDashboardDirectly()
	{
		// Vera organizes nothing in seed data - the home page's "Create an
		// organization" CTA opens org creation in place (#747: there is no
		// /app entry point to route through anymore), and submitting it must
		// still land her directly in the new org's dashboard.
		var frontend = Fixture.GetEndpoint("frontend");
		var orgName = $"Visual OrgAppEntry Empty {Guid.NewGuid():N}";

		// LoginAsync (real Keycloak login), not FastSignInAsync: creating an
		// org grants the "organisator" realm role server-side, but the
		// access token already in hand was minted before that grant, so the
		// very next request (OrgAppLayout's GetOrganizationDetails call,
		// gated by EinsatzbereitOrganisatorPolicy's static role claim) needs
		// a real signinSilent() token renewal to pick it up - see HomePage.tsx's
		// CreateOrganizationModal onSuccess handler. FastSignInAsync's seeded
		// session has no valid refresh token and no real Keycloak SSO cookie
		// for that renewal to succeed against (see its own doc comment), so
		// it would make this test unable to exercise the fix it depends on.
		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// fixture.ResetAsync() guarantees vera organizes nothing at this
		// point, so the CTA is deterministically the "create" button, not
		// the dashboard overview link.
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organization" });
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
}
