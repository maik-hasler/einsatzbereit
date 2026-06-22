using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ProfileOverviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfilePage_ShowsThreeTabButtons_WhenAuthenticated()
	{
		// Regression: /profile should render a tab bar instead of a flat page.
		// Introduced by PR #508 (RC.150 - unified profile overview).
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Profile" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Engagements" }))
			.ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Achievements" }))
			.ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task MyEngagements_Redirects_ToProfileEngagementsTab()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Should land on /profile?tab=engagements
		await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/profile\?tab=engagements"));

		// The page heading (h1) must still be visible after redirect
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task Achievements_Redirects_ToProfileAchievementsTab()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Should land on /profile?tab=achievements
		await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/profile\?tab=achievements"));

		// Share achievements button should be visible on the achievements tab
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}
}
