using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsScopeToggleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const double MaxWidthDeltaPx = 2;

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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Deutsch" }).ClickAsync();

		var upcomingTab = Page.GetByTestId("engagements-scope-upcoming");
		var pastTab = Page.GetByTestId("engagements-scope-past");
		await Expect(upcomingTab).ToContainTextAsync("Aktuell & Bevorstehend", new() { Timeout = 15_000 });
		await Expect(pastTab).ToContainTextAsync("Vergangen", new() { Timeout = 15_000 });

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
