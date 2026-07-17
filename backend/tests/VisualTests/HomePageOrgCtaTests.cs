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
	public async Task Authenticated_HeroOrgCta_OpensCreateOrganizationModalInPlace()
	{
		// #693: for a signed-in visitor the CTA must stay visible (previously it
		// vanished once authenticated) and open org creation directly on the
		// homepage, with no navigation away.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

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
}
