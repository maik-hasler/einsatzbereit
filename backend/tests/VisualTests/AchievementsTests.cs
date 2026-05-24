using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AchievementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ShareButton_OpensModal_WithQrCodeAndCopyLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var shareBtn = Page.GetByRole(AriaRole.Button,
			new() { Name = new System.Text.RegularExpressions.Regex("Errungenschaften teilen|Share achievements", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
		await Expect(shareBtn).ToBeVisibleAsync();
		await shareBtn.ClickAsync();

		var dialog = Page.Locator("[role=\"dialog\"]");
		await Expect(dialog).ToBeVisibleAsync();

		// QR code SVG is rendered inside the dialog
		await Expect(dialog.Locator("svg").First).ToBeVisibleAsync();

		// Share URL contains /achievements
		var dialogText = await dialog.TextContentAsync();
		await Expect(dialog.GetByRole(AriaRole.Button,
			new() { Name = new System.Text.RegularExpressions.Regex("Link kopieren|Copy link", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }))
			.ToBeVisibleAsync();
		Assert.That(dialogText, Does.Contain("/achievements"));
	}

	[Test]
	public async Task ShareModal_ClosesOnEscape()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button,
			new() { Name = new System.Text.RegularExpressions.Regex("Errungenschaften teilen|Share achievements", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })
			.ClickAsync();

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();

		await Page.Keyboard.PressAsync("Escape");

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeHiddenAsync();
	}

	[Test]
	public async Task ShareModal_ClosesOnBackdropClick()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button,
			new() { Name = new System.Text.RegularExpressions.Regex("Errungenschaften teilen|Share achievements", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })
			.ClickAsync();

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();

		// Click the backdrop (top-left corner, outside the dialog box)
		await Page.Mouse.ClickAsync(5, 5);

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeHiddenAsync();
	}
}
