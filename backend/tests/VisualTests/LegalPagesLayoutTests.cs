using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1127: the imprint and privacy policy pages rendered their
/// h1 without the shared `text-gray-900` color, and their body copy had no
/// width constraint inside AppLayout's `max-w-7xl` main - a roughly
/// 180-character line measure on desktop. Both pages now match the heading
/// color used everywhere else and wrap their content in a
/// `data-content-wrapper` reading-width column, left-aligned like the other
/// document-style pages (settings, profile) rather than centered like a
/// detail page - see VisualTestBase.AssertMaxWidthContentLeftAlignedAsync.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LegalPagesLayoutTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ImprintPage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.Locator("h1");
		await Expect(heading).ToBeVisibleAsync();
		await Expect(heading).ToContainClassAsync("text-gray-900");

		await AssertMaxWidthContentLeftAlignedAsync("Imprint page");
	}

	[Test]
	public async Task PrivacyPolicyPage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/privacy-policy");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.Locator("h1");
		await Expect(heading).ToBeVisibleAsync();
		await Expect(heading).ToContainClassAsync("text-gray-900");

		await AssertMaxWidthContentLeftAlignedAsync("Privacy policy page");
	}
}
