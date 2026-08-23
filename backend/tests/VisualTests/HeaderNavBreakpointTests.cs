using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderNavBreakpointTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int TabletWidth = 768;
	private const int DesktopWidth = 1024;
	private const int ViewportHeight = 1024;

	private static readonly string[] NavTestIds =
	[
		"nav-home",
		"nav-findOpportunities",
		"nav-forOrganizations",
		"nav-help",
	];

	[Test]
	public async Task HeaderNav_AtTabletWidth_HandsOverToTheBurgerInsteadOfWrapping()
	{
		await GoToHomePageInGermanAsync();

		await Page.SetViewportSizeAsync(TabletWidth, ViewportHeight);

		foreach (var testId in NavTestIds)
		{
			await Expect(Page.GetByTestId(testId)).ToBeHiddenAsync(new() { Timeout = 10_000 });
		}

		var burger = Page.GetByRole(AriaRole.Button, new() { Name = "Menü öffnen" });
		await Expect(burger).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await burger.ClickAsync();
		await Expect(Page.GetByTestId("mobile-nav-findOpportunities"))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task HeaderNav_AtTheDesktopBreakpoint_KeepsEveryGermanLabelOnOneLine()
	{
		await GoToHomePageInGermanAsync();

		await Page.SetViewportSizeAsync(DesktopWidth, ViewportHeight);

		var single = await SingleLineHeightAsync();

		foreach (var testId in NavTestIds)
		{
			var link = Page.GetByTestId(testId);
			await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });

			var box = await link.BoundingBoxAsync();
			box.Should().NotBeNull($"Could not measure the {testId} link");
			box!.Height.Should().BeApproximately(
				single,
				1f,
				$"{testId} must render on one line at {DesktopWidth}px - a taller box is a wrapped label");
		}

		var overflow = await Page.EvaluateAsync<int>(
			"() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
		overflow.Should().BeLessThanOrEqualTo(0, "the header must not push the page into horizontal scroll");
	}

	private async Task<float> SingleLineHeightAsync()
	{
		var box = await Page.GetByTestId("nav-help").BoundingBoxAsync();
		box.Should().NotBeNull("Could not measure the reference nav link");
		return box!.Height;
	}

	private async Task GoToHomePageInGermanAsync()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.GetLeftPart(UriPartial.Authority));
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" })
			.ClickAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("language-selector-menu")
			.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByTestId("nav-findOpportunities"))
			.ToHaveTextAsync("Einsätze finden", new() { Timeout = 10_000 });
	}
}
