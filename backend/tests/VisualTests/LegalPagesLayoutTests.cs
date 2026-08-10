using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Layout guards for the four standalone public info pages (imprint, privacy
/// policy, terms of use, contact).
///
/// Originally #1127/#1673: these pages rendered an unconstrained ~180-character
/// line measure, fixed by wrapping their body in a `data-content-wrapper`
/// reading column that sat flush left inside `&lt;main&gt;` (#766).
///
/// #1755 replaced that with a title band plus a centred document column,
/// because flush-left was the reason the pages read as a dead strip against
/// ~830px of empty page on desktop. The `data-content-wrapper` contract from
/// #1328 is unchanged - only the invariant it has to satisfy flipped from
/// left-aligned to centred, so these tests now assert the new arrangement
/// rather than the one it replaced.
///
/// The third assertion is the one worth having: the band's h1 and the content
/// column below it have to share a left edge. They are separately positioned
/// (the band breaks out of `&lt;main&gt;` to run edge-to-edge, then re-derives
/// the column geometry internally), so nothing structural keeps them in sync -
/// and the first cut of #1755 shipped them 175px apart.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LegalPagesLayoutTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ImprintPage_TitleBandAlignsWithCentredContentColumn()
	{
		await AssertLegalPageLayoutAsync("/imprint", "Imprint page");
	}

	[Test]
	public async Task PrivacyPolicyPage_TitleBandAlignsWithCentredContentColumn()
	{
		await AssertLegalPageLayoutAsync("/privacy-policy", "Privacy policy page");
	}

	[Test]
	public async Task TermsOfUsePage_TitleBandAlignsWithCentredContentColumn()
	{
		await AssertLegalPageLayoutAsync("/terms-of-use", "Terms of use page");
	}

	[Test]
	public async Task ContactPage_TitleBandAlignsWithCentredContentColumn()
	{
		await AssertLegalPageLayoutAsync("/contact", "Contact page");
	}

	private async Task AssertLegalPageLayoutAsync(string path, string label)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// Wide enough that a centred max-w-5xl column is distinguishable from a
		// flush-left one - at a narrow viewport the column fills the available
		// width and both arrangements look identical.
		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{path}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.Locator("h1");
		await Expect(heading).ToBeVisibleAsync();
		// White on the brand-800 band, not the text-gray-900 the pages used
		// when the title sat on plain white.
		await Expect(heading).ToContainClassAsync("text-white");

		await AssertMaxWidthContentCenteredAsync(label);

		var edgeDelta = 0d;
		await PollUntilAsync(async () =>
		{
			edgeDelta = await Page.EvaluateAsync<double>(
				"""
				() => {
					const h1 = document.querySelector('main h1');
					const column = document.querySelector('main [data-content-wrapper]');
					return Math.abs(h1.getBoundingClientRect().left
						- column.getBoundingClientRect().left);
				}
				""");
			return edgeDelta < 2;
		}, () => $"{label}: the title band's h1 should share a left edge with the content "
			+ $"column below it (last observed delta = {edgeDelta}px, must be <2px)");
	}
}
