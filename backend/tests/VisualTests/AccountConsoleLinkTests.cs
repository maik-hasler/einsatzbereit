using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1675: the "Account Settings" entry (added by #1098) linked out to
/// Keycloak's own, unbranded account console (`${authority}/account`), which
/// the realm never actually provisions a client for and which currently
/// errors on staging. Everything it uniquely offered is either already
/// reachable branded (password reset, profile editing at /profile) or not
/// configured in the realm at all (2FA, session management) - so the entry
/// point itself is removed rather than themed. This guards against it
/// reappearing in either menu.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccountConsoleLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountDropdown_AuthenticatedUser_HasNoKeycloakAccountConsoleLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My profile" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Account Settings" })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task MobileMenu_AuthenticatedUser_HasNoKeycloakAccountConsoleLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// FastSignInAsync's own "User menu" wait needs the desktop-width nav
		// visible (DesktopHeader.tsx's "hidden md:flex") - sign in at the
		// default (desktop-sized) viewport, then shrink down to mobile only
		// afterward, mirroring MobileHeaderTests' own viewport handling.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(390, 844);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My profile" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Account Settings" })).Not.ToBeVisibleAsync();
	}
}
