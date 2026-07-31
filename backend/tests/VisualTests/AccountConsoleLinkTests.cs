using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1098: users had no in-app way to change their password or email - the
/// account dropdown (desktop) and mobile burger menu only ever linked to
/// /profile and (admins only) /administration. This adds an "Account
/// Settings" entry in both menus pointing at Keycloak's own account console
/// (`${authority}/account`), which already supports password/email changes,
/// rather than building that functionality natively.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccountConsoleLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountDropdown_AuthenticatedUser_LinksToKeycloakAccountConsole()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var authority = $"{Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/')}/realms/einsatzbereit";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).ClickAsync();

		var link = Page.GetByRole(AriaRole.Link, new() { Name = "Account Settings" });
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(link).ToHaveAttributeAsync("href", $"{authority}/account");
		await Expect(link).ToHaveAttributeAsync("target", "_blank");
		await Expect(link).ToHaveAttributeAsync("rel", "noopener noreferrer");
	}

	[Test]
	public async Task MobileMenu_AuthenticatedUser_LinksToKeycloakAccountConsole()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var authority = $"{Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/')}/realms/einsatzbereit";

		// FastSignInAsync's own "User menu" wait needs the desktop-width nav
		// visible (DesktopHeader.tsx's "hidden md:flex") - sign in at the
		// default (desktop-sized) viewport, then shrink down to mobile only
		// afterward, mirroring MobileHeaderTests' own viewport handling.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var link = Page.GetByRole(AriaRole.Link, new() { Name = "Account Settings" });
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(link).ToHaveAttributeAsync("href", $"{authority}/account");
		await Expect(link).ToHaveAttributeAsync("target", "_blank");
		await Expect(link).ToHaveAttributeAsync("rel", "noopener noreferrer");
	}
}
