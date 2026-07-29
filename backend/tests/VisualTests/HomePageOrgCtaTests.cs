using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

// #1316: needs vera to deterministically have zero organizations - opts into
// fixture.ResetAsync() and a keyed [NotInParallel] so only other classes
// sharing the "visualtests-db" key (not the whole 207-test assembly) are
// excluded while this one mutates her organization membership.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
public class HomePageOrgCtaTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task Anonymous_HeroOrgCta_RedirectsToKeycloakRegistrationEndpoint()
	{
		// #693: the hero's second CTA is labelled "Create an organisation" - it must
		// behave like the header's "Register" button (registration, not a plain login),
		// and it must stay visible for anonymous visitors (this is the case here).
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" })
			.First.ClickAsync();

		await Expect(Page).ToHaveURLAsync(
			new Regex(@"/realms/einsatzbereit/protocol/openid-connect/registrations"));
		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}

	[Test]
	public async Task Authenticated_WithoutOrgs_HeroOrgCta_OpensCreateOrganizationModalInPlace()
	{
		// #693: for a signed-in visitor with no orgs yet, the CTA must stay
		// visible and open org creation directly on the homepage, with no
		// navigation away. Once a user organizes an org, the CTA instead
		// becomes an "Organization overview" link (see the test below).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// fixture.ResetAsync() guarantees vera organizes nothing at this
		// point, so the CTA is deterministically the "create" button, not
		// the dashboard overview link.
		var cta = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" });
		await Expect(cta.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await cta.First.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(dialog.GetByText("Create organization")).ToBeVisibleAsync();

		await Expect(Page).ToHaveURLAsync($"{origin}/");

		await Page.Locator("[data-testid='modal-cancel']").ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task Authenticated_WithOrgs_HeroOrgCta_LinksToOrgOverviewInstead()
	{
		// Olaf already organizes orgs in seed data - the hero CTA must swap to
		// an "Organization overview" link that resolves straight to his dashboard
		// (#747: the /app intermediate picker page no longer exists) instead of
		// offering to create yet another one from the homepage.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" }))
			.Not.ToBeVisibleAsync();

		var cta = Page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await Expect(cta.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await cta.First.ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}

	[Test]
	public async Task Authenticated_OrgsFetchFails_HeroCta_NeverShowsCreateBranch()
	{
		// HomePage used to destructure only useSharedOrgFetch's data slot, so
		// "still loading" and "fetch failed" both collapsed into the same
		// orgsData == null -> orgs == [] state as a genuine zero-orgs signed-in
		// user. Olaf organizes orgs in seed data, so if his org-list fetch
		// fails he must never see the "create an organisation" CTA - clicking
		// it would have created a duplicate of an org he already had. Asserts
		// the contract (the create-org branch must never appear while the
		// fetch has failed), not any particular recovery mechanism - this
		// stays valid whether or not a retry is ever added underneath.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.RouteAsync("**/v1/organizations", route => route.AbortAsync());

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" }))
			.Not.ToBeVisibleAsync();
	}
}
