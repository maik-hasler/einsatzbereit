using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccountPageHeaderScaleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	[Arguments("/profile", "My profile")]
	[Arguments("/profile/settings", "Settings")]
	[Arguments("/my-signups", "My sign-ups")]
	public async Task AccountPage_RendersCompactTitle_NotTheMarketingHeroScale(string path, string title)
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}{path}");

		var heading = Page.Locator("main").GetByRole(AriaRole.Heading, new() { Name = title, Level = 1 });
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var headingFontSizePx = await heading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		headingFontSizePx.Should().BeLessThan(48,
			$"{path} holds a short form or a thin list, not an introduction - " +
			"the band's brand surface stays, but compactTitle narrows the h1");

		await Expect(Page.Locator("main .bg-brand-800")).ToHaveCountAsync(1);
		await Expect(Page.Locator("main svg[viewBox='0 0 1440 60']")).ToHaveCountAsync(1);
		await Expect(Page.Locator("header + div nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);
	}

	[Test]
	[Arguments("/help", "Help")]
	[Arguments("/imprint", "Imprint")]
	public async Task LegalOrHelpPage_KeepsTheFullMarketingHeroScale_Unaffected(string path, string title)
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}{path}");

		var heading = Page.Locator("main").GetByRole(AriaRole.Heading, new() { Name = title, Level = 1 });
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var headingFontSizePx = await heading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		headingFontSizePx.Should().BeGreaterThanOrEqualTo(48,
			$"{path} is the page family PageHeaderBand's hero scale was built for (#1755) - " +
			"#1841 narrows compactTitle pages only, it does not revisit this");
	}
}
