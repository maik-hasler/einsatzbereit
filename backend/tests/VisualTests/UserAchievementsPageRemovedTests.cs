using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1032: /users/{userId}/achievements duplicated content
/// UserProfilePage.tsx already rendered in full (same BadgeGrid, same
/// catalog + earned data). The route, UserAchievementsPage.tsx, the
/// GetUserAchievements backend endpoint, and the "View all achievements"
/// link were all removed rather than differentiated, since the two pages
/// showed identical content.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class UserAchievementsPageRemovedTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Vera is a plain "user" account, not an organizer, so FastSignInAsync's
	// returned pinned-org id is always null for her - the actual user id has
	// to be read back out of the seeded oidc-client-ts session instead.
	private static async Task<string?> GetSignedInUserIdAsync(IPage page) =>
		await page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");

	[Test]
	public async Task OldAchievementsRoute_NoLongerExists_RendersNotFoundPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		var userId = await GetSignedInUserIdAsync(Page);
		Skip.When(userId is null, "could not resolve the logged-in user's id");

		await Page.GotoAsync($"{origin}/users/{userId}/achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task UserProfilePage_ShowsBadgesDirectly_WithNoLinkToRemovedAchievementsPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		var userId = await GetSignedInUserIdAsync(Page);
		Skip.When(userId is null, "could not resolve the logged-in user's id");

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "View all achievements" }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.Locator($"a[href='/users/{userId}/achievements']"))
			.Not.ToBeVisibleAsync();
	}
}
