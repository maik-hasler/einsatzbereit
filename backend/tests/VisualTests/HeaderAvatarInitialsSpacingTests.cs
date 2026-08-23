using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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
