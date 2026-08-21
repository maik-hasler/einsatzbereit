using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1836: the /my-signups scope toggle ("Current &amp; upcoming" /
/// "Past") is a hand-rolled segmented control whose two segments sized to their
/// own label content only (no width-equalizing utility), so the longer
/// "Current &amp; upcoming" segment rendered visibly wider than "Past" - nearly
/// 2:1 on live staging (162px vs. 87px in a 259px track). Fixed by making the
/// track a CSS Grid with two minmax(0,1fr) columns, so it renders both segments
/// at the width of the wider one regardless of label length. An equal-width
/// flex-1 track (the original #1836 fix) has the same shrink-to-fit total
/// width, so it instead squeezes both segments to half that width - which can
/// compress the longer label below its own single-line width and wrap it, so
/// grid is the fix that keeps both goals (equal width, no forced wrap) at once
/// in a longer locale like German's "Aktuell &amp; Bevorstehend" / "Vergangen".
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeToggleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Both segments share the same grid track width, so their rendered widths
	// should match modulo getBoundingClientRect's sub-pixel rounding - far
	// below the ~75px gap the unequal-width bug produced.
	private const double MaxWidthDeltaPx = 2;

	// A wrapped-to-two-lines segment renders roughly twice as tall as its
	// single-line sibling - far above sub-pixel rounding - so an equal-height
	// check across the pair is a direct proxy for "neither one wrapped".
	private const double MaxHeightDeltaPx = 2;

	[Test]
	public async Task ScopeToggle_RendersUpcomingAndPastSegments_AtEqualWidth()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var upcomingTab = Page.GetByTestId("engagements-scope-upcoming");
		var pastTab = Page.GetByTestId("engagements-scope-past");
		await Expect(upcomingTab).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(pastTab).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Both widths read in a single EvaluateAsync call rather than two
		// separate BoundingBoxAsync round trips, so nothing can reflow between
		// the two reads (see VisualTestBase.AssertMaxWidthContentCenteredAsync
		// for the same pattern).
		var widthDelta = 0d;
		var upcomingWidth = 0d;
		var pastWidth = 0d;
		await PollUntilAsync(async () =>
		{
			var widths = await Page.EvaluateAsync<double[]>(
				"""
				() => {
					const upcoming = document.querySelector("[data-testid='engagements-scope-upcoming']");
					const past = document.querySelector("[data-testid='engagements-scope-past']");
					return [upcoming.getBoundingClientRect().width, past.getBoundingClientRect().width];
				}
				""");
			upcomingWidth = widths[0];
			pastWidth = widths[1];
			widthDelta = Math.Abs(upcomingWidth - pastWidth);
			return widthDelta < MaxWidthDeltaPx;
		}, () => "myEngagements scope toggle: 'Current & upcoming' and 'Past' segments should render at "
			+ $"equal width (last observed upcoming={upcomingWidth:F1}px, past={pastWidth:F1}px, "
			+ $"delta={widthDelta:F1}px, must be <{MaxWidthDeltaPx}px)");
	}

	[Test]
	public async Task ScopeToggle_German_RendersUpcomingAndPastSegmentsOnASingleLine()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// FastSignInAsync itself waits on the English "User menu" aria-label
		// (see OrgDashboardWidgetsTests), so switch language only afterwards.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		var upcomingTab = Page.GetByTestId("engagements-scope-upcoming");
		var pastTab = Page.GetByTestId("engagements-scope-past");
		await Expect(upcomingTab).ToContainTextAsync("Aktuell & Bevorstehend", new() { Timeout = 15_000 });
		await Expect(pastTab).ToContainTextAsync("Vergangen", new() { Timeout = 15_000 });

		// Widths and heights read in a single EvaluateAsync call rather than
		// separate BoundingBoxAsync round trips, so nothing can reflow between
		// the reads (see VisualTestBase.AssertMaxWidthContentCenteredAsync for
		// the same pattern).
		var widthDelta = 0d;
		var heightDelta = 0d;
		var upcomingWidth = 0d;
		var pastWidth = 0d;
		var upcomingHeight = 0d;
		var pastHeight = 0d;
		await PollUntilAsync(async () =>
		{
			var rects = await Page.EvaluateAsync<double[]>(
				"""
				() => {
					const upcoming = document.querySelector("[data-testid='engagements-scope-upcoming']").getBoundingClientRect();
					const past = document.querySelector("[data-testid='engagements-scope-past']").getBoundingClientRect();
					return [upcoming.width, past.width, upcoming.height, past.height];
				}
				""");
			upcomingWidth = rects[0];
			pastWidth = rects[1];
			upcomingHeight = rects[2];
			pastHeight = rects[3];
			widthDelta = Math.Abs(upcomingWidth - pastWidth);
			heightDelta = Math.Abs(upcomingHeight - pastHeight);
			return widthDelta < MaxWidthDeltaPx && heightDelta < MaxHeightDeltaPx;
		}, () => "myEngagements scope toggle (German): 'Aktuell & Bevorstehend' and 'Vergangen' segments should "
			+ "render at equal width and height, i.e. neither wraps to a second line (last observed "
			+ $"upcoming={upcomingWidth:F1}x{upcomingHeight:F1}px, past={pastWidth:F1}x{pastHeight:F1}px, "
			+ $"widthDelta={widthDelta:F1}px, heightDelta={heightDelta:F1}px, both must be <2px)");
	}
}
