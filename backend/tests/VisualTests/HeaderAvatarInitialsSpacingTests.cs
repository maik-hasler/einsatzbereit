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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var avatar = Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).Locator("span").First;
		await Expect(avatar).ToHaveTextAsync("VV");

		var letterSpacing = await avatar.EvaluateAsync<string>("el => getComputedStyle(el).letterSpacing");
		letterSpacing.Should().NotBe("normal",
			"two identical letters need extra spacing at this size or they read as one fused glyph (#1915)");
	}

	[Test]
	public async Task MobileMenuAvatar_HasLetterSpacing_ForTwoLetterInitials()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).ClickAsync();

		var menu = Page.GetByRole(AriaRole.Dialog, new() { Name = "Menu" });
		var avatar = menu.GetByText("VV", new() { Exact = true });
		await Expect(avatar).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var letterSpacing = await avatar.EvaluateAsync<string>("el => getComputedStyle(el).letterSpacing");
		letterSpacing.Should().NotBe("normal",
			"two identical letters need extra spacing at this size or they read as one fused glyph (#1915)");
	}
}
