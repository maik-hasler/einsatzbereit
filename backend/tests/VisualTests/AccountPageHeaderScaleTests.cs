using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #1841: the account pages (/profile, /profile/settings,
/// /my-signups) reused PageHeaderBand's full marketing-hero type scale
/// (72px, the same treatment HomePage's hero uses) for a two-word page title
/// sitting above a short form or a thin list - disproportionate to that
/// content, on the same band the legal/help/contact pages use to read as
/// still-the-same-product on arrival from the landing page's footer (#1755).
///
/// PageHeaderBand's new `compactTitle` prop narrows the type scale for just
/// that page family, keeping the band's brand surface (colour, wave cap, no
/// BreadcrumbBar) unchanged - this is the one thing #1841 asked not to
/// revisit. The legal/help pages therefore get their own assertion here too,
/// pinning that the hero scale survives for the family this band was
/// originally built for.
/// </summary>
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

		// Same 48px threshold OrgAppCompactHeaderTests uses for the org app's
		// own compact header - well under the band's default lg:text-7xl
		// (72px) and comfortably above the compact text-3xl/sm:text-4xl scale
		// (30px/36px) this page family now renders at.
		var headingFontSizePx = await heading.EvaluateAsync<double>(
			"el => parseFloat(getComputedStyle(el).fontSize)");
		headingFontSizePx.Should().BeLessThan(48,
			$"{path} holds a short form or a thin list, not an introduction - " +
			"the band's brand surface stays, but compactTitle narrows the h1");

		// The brand surface itself - the one thing #1841 explicitly did not
		// revisit - is unchanged: still the same dark band, wave cap and no
		// BreadcrumbBar restating the title a second time.
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
