using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1915: at the header user-menu avatar's 36px/bold size, two identical
/// adjacent initials (e.g. Vera Volunteer's "VV") visually fuse into an
/// unrelated glyph even though the underlying text is still correct - a
/// kerning/rendering issue no text-content assertion alone would catch.
/// AccountControls (desktop) and MobileMenu (mobile) both add letter-spacing
/// to that avatar's span to keep it legible; this asserts the fix is
/// actually applied on both, since no CI browser here can judge the result
/// by eye.
///
/// Signed in as Olaf ("OO"), not Vera: AvatarAndLogoDisplayTests.cs uploads
/// and removes Vera's own avatar_url elsewhere in this suite (shared
/// PerTestSession fixture), which would intermittently swap her header
/// avatar for an &lt;img&gt; and make the initials span disappear out from
/// under this test. Nothing in the suite ever uploads Olaf's personal
/// avatar (only organization logos), so "OO" is a deterministic two-letter
/// case - the fix itself is unconditional on which two letters are shown.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderAvatarInitialsSpacingTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 390;
	private const int MobileHeight = 844;

	[Test]
	public async Task DesktopUserMenuAvatar_HasLetterSpacing_ForTwoLetterInitials()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var avatar = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).Locator("span").First;
		await Expect(avatar).ToHaveTextAsync("OO");

		var letterSpacing = await avatar.EvaluateAsync<string>("el => getComputedStyle(el).letterSpacing");
		letterSpacing.Should().NotBe("normal",
			"two identical letters need extra spacing at this size or they read as one fused glyph (#1915)");
	}

	[Test]
	public async Task MobileMenuAvatar_HasLetterSpacing_ForTwoLetterInitials()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// FastSignInAsync's own "User menu" wait needs the desktop-width nav
		// visible (DesktopHeader.tsx's "hidden md:flex") - sign in at the
		// default (desktop-sized) viewport, then shrink down to mobile only
		// afterward, mirroring AccountConsoleLinkTests's own viewport handling.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		var menu = Page.GetByRole(AriaRole.Dialog, new() { Name = "Menu" });
		var avatar = menu.GetByText("OO", new() { Exact = true });
		await Expect(avatar).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var letterSpacing = await avatar.EvaluateAsync<string>("el => getComputedStyle(el).letterSpacing");
		letterSpacing.Should().NotBe("normal",
			"two identical letters need extra spacing at this size or they read as one fused glyph (#1915)");
	}
}
