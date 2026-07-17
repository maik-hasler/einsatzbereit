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

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var cta = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" });
		if (await cta.CountAsync() == 0)
			return; // a previous retry already gave vera an org - skip

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
		// an "Organization overview" link into /app instead of offering to
		// create yet another one from the homepage.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create an organisation" }))
			.Not.ToBeVisibleAsync();

		var cta = Page.GetByRole(AriaRole.Link, new() { Name = "Organization overview" });
		await Expect(cta.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await cta.First.ClickAsync();

		await Expect(Page).ToHaveURLAsync($"{origin}/app");
	}
}
