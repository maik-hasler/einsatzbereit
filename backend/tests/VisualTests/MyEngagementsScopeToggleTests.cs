using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1836: the /my-signups scope toggle ("Current &amp; upcoming" /
/// "Past") is a hand-rolled segmented control whose two segments sized to their
/// own label content only (no width-equalizing utility), so the longer
/// "Current &amp; upcoming" segment rendered visibly wider than "Past" - nearly
/// 2:1 on live staging (162px vs. 87px in a 259px track). Fixed by adding
/// flex-1/text-center to both buttons so the track splits evenly regardless of
/// label length.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeToggleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Both segments share flex-1 on the same track, so their rendered widths
	// should match modulo getBoundingClientRect's sub-pixel rounding - far
	// below the ~75px gap the unequal-width bug produced.
	private const double MaxWidthDeltaPx = 2;

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
}
