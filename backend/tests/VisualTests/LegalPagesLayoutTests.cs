using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1127 (imprint, privacy policy) and #1673 (terms of use,
/// contact): these four static legal/info pages rendered their h1 without
/// the shared `text-gray-900` color, and their body copy had no width
/// constraint inside AppLayout's `max-w-7xl` main - a roughly 180-character
/// line measure on desktop. All four now match the heading color used
/// everywhere else and wrap their content in a `data-content-wrapper`
/// reading-width column, left-aligned like the other document-style pages
/// (settings, profile) rather than centered like a detail page - see
/// VisualTestBase.AssertMaxWidthContentLeftAlignedAsync.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LegalPagesLayoutTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ImprintPage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		await AssertLegalPageLayoutAsync("/imprint", "Imprint page");
	}

	[Test]
	public async Task PrivacyPolicyPage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		await AssertLegalPageLayoutAsync("/privacy-policy", "Privacy policy page");
	}

	[Test]
	public async Task TermsOfUsePage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		await AssertLegalPageLayoutAsync("/terms-of-use", "Terms of use page");
	}

	[Test]
	public async Task ContactPage_HeadingMatchesSharedScale_AndContentIsLeftAlignedWithinMain()
	{
		await AssertLegalPageLayoutAsync("/contact", "Contact page");
	}

	private async Task AssertLegalPageLayoutAsync(string path, string label)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{path}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.Locator("h1");
		await Expect(heading).ToBeVisibleAsync();
		await Expect(heading).ToContainClassAsync("text-gray-900");

		await AssertMaxWidthContentLeftAlignedAsync(label);
	}
}
