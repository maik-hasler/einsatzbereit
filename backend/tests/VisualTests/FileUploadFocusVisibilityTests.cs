using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class FileUploadFocusVisibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Every file picker in the app pairs a visually hidden `input[type=file]` with
	// the label that stands in for it, so the browser drew the focus ring around a
	// 1x1 clipped box and keyboard focus simply vanished for a tab stop (#2327).
	// global.css projects the ring onto the label instead; only a real browser
	// resolves `:has()` and `:focus-visible`, so the check belongs here.
	[Test]
	public async Task AvatarUpload_WhenFocusedFromTheKeyboard_ShowsTheRingOnItsLabel()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.GetByTestId("profile-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Page.GetByTestId("profile-edit").ClickAsync();

		var label = Page.Locator("label[for='avatar-upload']");
		await Expect(label).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var outlineWhileBlurred = await OutlineWidthAsync(label);
		outlineWhileBlurred.Should().Be(0,
			"the upload label must only carry a ring while its input holds focus");

		// The keypress puts Chromium into keyboard-interaction mode, which is what
		// makes the focus that follows match `:focus-visible` rather than plain
		// `:focus` - the same distinction a mouse click on the label relies on.
		await Page.Keyboard.PressAsync("Tab");
		await Page.Locator("#avatar-upload").FocusAsync();

		await Expect(Page.Locator("#avatar-upload")).ToBeFocusedAsync();

		var outlineWhileFocused = await OutlineWidthAsync(label);
		outlineWhileFocused.Should().BeGreaterThan(0,
			"focus on the visually hidden file input must be visible on its label");
	}

	private static async Task<double> OutlineWidthAsync(ILocator locator) =>
		await locator.EvaluateAsync<double>(@"el => {
			const style = getComputedStyle(el);
			if (style.outlineStyle === 'none') return 0;
			return parseFloat(style.outlineWidth) || 0;
		}");
}
