using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Issue #1793: the header's desktop nav swapped to the burger at <c>md</c>
/// (768px), but everything the bar carries measures ~904px wide with the
/// German labels - so from 768px up to ~951px the full desktop nav rendered
/// into a row too narrow for it and "Einsaetze finden" / "Fuer Organisationen"
/// each broke across two lines, leaving a ragged two-line header on the most
/// common tablet width. The swap is at <c>lg</c> (1024px) now, the first width
/// where the labels actually fit.
///
/// German is the constraint and the default served locale; the English labels
/// are short enough to fit either way, which is why these tests switch the app
/// to German first rather than asserting against the shorter default.
/// </summary>
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

		// The desktop bar is gone in its entirety - links, sign-in/register
		// pair and language selector alike - rather than squeezed.
		foreach (var testId in NavTestIds)
		{
			await Expect(Page.GetByTestId(testId)).ToBeHiddenAsync(new() { Timeout = 10_000 });
		}

		// ...and the burger is what a tablet visitor gets instead, opening the
		// panel that carries the same destinations. Both halves matter: the
		// desktop nav and the burger strip swap on one shared breakpoint, so a
		// mismatch between them would leave this width with no navigation at all.
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

		// Exactly at the breakpoint - Tailwind's min-width is inclusive, so
		// 1024 is the narrowest width that renders the desktop bar and thus the
		// tightest fit it ever has to survive.
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

		// whitespace-nowrap turns a future too-long label into an overflowing
		// row rather than a wrapped one, so pin the row's fit as well: a
		// horizontally scrolling page would be the next shape of this bug.
		var overflow = await Page.EvaluateAsync<int>(
			"() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
		overflow.Should().BeLessThanOrEqualTo(0, "the header must not push the page into horizontal scroll");
	}

	/// <summary>
	/// "Hilfe" is a single short word that cannot wrap at any width, so its own
	/// rendered height is the one-line reference the other three are measured
	/// against - self-calibrating, rather than a hardcoded pixel height that
	/// would need updating whenever the link padding or type scale changes.
	/// </summary>
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

		// Switched through the header control rather than seeded into
		// localStorage before the first load: each language's JSON is fetched
		// lazily (see NavigationTests), and going through the selector waits on
		// that fetch the same way a real visitor's switch does.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" })
			.ClickAsync(new() { Timeout = 15_000 });
		// A plain <button> inside the selector's <ul>, not an option: #1825 dropped
		// the listbox/option roles this component never implemented the keyboard
		// model for. Scoped to the open menu so it cannot match anything else.
		await Page.GetByTestId("language-selector-menu")
			.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		await Expect(Page.GetByTestId("nav-findOpportunities"))
			.ToHaveTextAsync("Einsätze finden", new() { Timeout = 10_000 });
	}
}
