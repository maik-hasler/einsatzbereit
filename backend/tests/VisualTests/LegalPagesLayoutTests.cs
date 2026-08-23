using Microsoft.Playwright;

namespace VisualTests;

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

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{path}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.Locator("h1");
		await Expect(heading).ToBeVisibleAsync();

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
