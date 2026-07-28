using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #992: the app defined no custom :focus-visible ring, so
/// keyboard-focus appearance depended entirely on the browser default -
/// invisible in engines that render outline-color 'auto' as white on a
/// white/light surface. The homepage's "Browse organizations" CTA
/// additionally suppressed the outline entirely via a low-contrast,
/// ring-only focus style (outline: none with no visible fallback), a direct
/// WCAG 2.4.7 failure. Both cases now resolve to the single shared
/// :focus-visible token in global.css.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class FocusVisibleRingTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// brand-700 (#226947), the shared --focus-ring-color token in global.css.
	private const string ExpectedRingColor = "rgb(34, 105, 71)";

	[Test]
	public async Task HomePage_OrgDirectoryCta_ShowsVisibleFocusRingOnKeyboardFocus()
	{
		// This CTA (Button.tsx) used to pair focus-visible:outline-none with a
		// 30%-opacity ring - reported by the live audit as outlineStyle 'none'
		// with no adequate replacement.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var cta = Page.Locator("[data-testid='organizations-teaser-cta']");
		await Expect(cta).ToBeVisibleAsync();
		await TabToAsync(cta);

		var outlineStyle = await cta.EvaluateAsync<string>("el => getComputedStyle(el).outlineStyle");
		var outlineColor = await cta.EvaluateAsync<string>("el => getComputedStyle(el).outlineColor");

		outlineStyle.Should().Be("solid", "the CTA must no longer suppress its focus outline entirely");
		outlineColor.Should().Be(ExpectedRingColor);
	}

	[Test]
	public async Task Footer_BrowseOrganizationsLink_ShowsVisibleFocusRingOnDarkSurface()
	{
		// The footer (bg-brand-800, the same dark surface as the homepage hero)
		// styles none of its links for focus - before this fix they relied
		// entirely on the browser default outline, which is not guaranteed to
		// contrast against a dark background.
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var link = Page.Locator("footer").GetByRole(AriaRole.Link, new() { Name = "Browse organizations" });
		await Expect(link).ToBeVisibleAsync();
		await TabToAsync(link);

		var outlineStyle = await link.EvaluateAsync<string>("el => getComputedStyle(el).outlineStyle");
		var outlineColor = await link.EvaluateAsync<string>("el => getComputedStyle(el).outlineColor");
		var boxShadow = await link.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");

		outlineStyle.Should().Be("solid");
		outlineColor.Should().Be(ExpectedRingColor);
		// The white halo is what keeps the ring visible against this dark
		// footer background (a flat brand-700 outline alone is too close in
		// luminance to bg-brand-800 to clear 3:1 contrast).
		boxShadow.Should().Contain("255, 255, 255");
	}

	/// <summary>
	/// Locator.FocusAsync() calls the DOM focus() method directly, which does
	/// not set the browser's internal "focus came from the keyboard" flag -
	/// :focus-visible then never matches and every assertion here would
	/// observe the un-focused, un-ringed element instead. Real Tab keypresses
	/// (as a keyboard-only visitor would use) do set that flag, so drive
	/// focus that way instead - bounded so a missing/unreachable target fails
	/// fast rather than hanging.
	/// </summary>
	private async Task TabToAsync(ILocator target, int maxPresses = 40)
	{
		for (var i = 0; i < maxPresses; i++)
		{
			await Page.Keyboard.PressAsync("Tab");
			if (await target.EvaluateAsync<bool>("el => el === document.activeElement"))
				return;
		}

		throw new Exception($"Could not reach the target element via Tab within {maxPresses} presses.");
	}
}
