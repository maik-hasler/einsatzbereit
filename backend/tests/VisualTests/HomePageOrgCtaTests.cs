using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HomePageOrgCtaTests(AspireFixture fixture) : VisualTestBase(fixture)
{
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

		// vera is shared across this whole test session (no DB reset between
		// tests), so another test (e.g. OrganizationTests inviting her as a
		// member elsewhere) can give her an org at any point up to and
		// including the moment we click. Wait for whichever of the two the
		// org-count fetch actually resolves to, rather than asserting the
		// button specifically - a separate, later Expect on just the button
		// re-opens exactly this race, which is what actually broke this
		// test previously (see git blame).
		var cta = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" });
		var overviewLink = Page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await Expect(cta.Or(overviewLink).First).ToBeVisibleAsync(new() { Timeout = 10_000 });

		if (await overviewLink.CountAsync() > 0)
			return; // vera already organizes an org - skip, nothing to exercise here

		try
		{
			await cta.First.ClickAsync(new() { Timeout = 5_000 });
		}
		catch (TimeoutException)
		{
			// The org-count fetch can still resolve and swap the button out
			// for the dashboard Link in the narrow window right as we click -
			// if that happened, vera already has an org, so this is the same
			// "skip" case as the check above, not a real failure.
			if (await cta.CountAsync() == 0)
				return;
			throw;
		}

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
}
