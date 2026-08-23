using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

// The org-name-link case (#2162) moved to
// F/pages/MyEngagementsPage/ActivitySection.test.tsx - it was pure rendering
// of an already-fetched engagement, no browser required. This one case stays:
// its assertion is a rendered box height (sr-only vs. visually duplicated
// heading), which jsdom has no layout engine to measure.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MyEngagementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagementsPage_StatesItsTitleOnce_WithADistinctSrOnlyInContentHeading()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My sign-ups", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.Locator("#activity").GetByRole(AriaRole.Heading, new() { Name = "My sign-ups" }))
			.ToHaveCountAsync(0);

		var inContentTitle = Page.Locator("#activity")
			.GetByRole(AriaRole.Heading, new() { Name = "Sign-ups list" });
		await Expect(inContentTitle).ToHaveCountAsync(1);

		var box = await inContentTitle.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(4,
			"the in-content heading must stay sr-only - a second visible copy of the <h1> is what #1796 removed");

		await Expect(Page.GetByRole(AriaRole.Group, new() { Name = "Time range" }))
			.ToBeVisibleAsync();
	}
}
