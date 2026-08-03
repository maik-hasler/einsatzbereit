using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1034: useAchievementNotifier (the badge-unlock toast poller)
/// was only ever mounted in AppLayout, the public-site layout - OrgAppLayout/
/// OrgAppShell (the org app shell at /app/{organizationId}/...) never called it,
/// so an organizer working inside the org app never polled for newly-unlocked
/// badges and never saw the unlock toast there, even though the exact same
/// account would see it on the public site.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppAchievementNotifierTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EnteringOrgAppShell_TriggersItsOwnAchievementsCheck()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// Registered before FastSignInAsync's own navigation - Page.WaitForResponseAsync
		// must start listening before the request fires, not after (see
		// AchievementsTests.cs for the same pattern). This is AppLayout's own
		// on-mount GET /v1/me/achievements, fired from the home page
		// FastSignInAsync lands on - not this test's subject, but it must be
		// allowed to resolve before arming the second wait below, or that
		// second wait could spuriously match this same, first, request.
		var homePageAchievementsCheckTask = Page.WaitForResponseAsync(
			r => r.Url.Contains("/v1/me/achievements") && r.Request.Method == "GET");

		// olaf organizes a seeded org (see AuthHelper.FastSignInAsync's doc
		// comment on GetPinnedOrganizerOrganizationId).
		var organizationId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		organizationId.Should().NotBeNull("olaf organizes a seeded org in this test environment");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await homePageAchievementsCheckTask;

		// Armed only after the home page's own initial check (above) has
		// already resolved, so this can only match the *next* occurrence -
		// the org app shell's own, independent mount of the same hook, not a
		// second wait racing that same first request.
		var orgShellAchievementsCheckTask = Page.WaitForResponseAsync(
			r => r.Url.Contains("/v1/me/achievements") && r.Request.Method == "GET");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, organizationId!.Value);
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Before the #1034 fix, OrgAppLayout/OrgAppShell never mounted
		// useAchievementNotifier at all, so entering the org app shell never
		// fired this GET a second time and this wait timed out.
		await orgShellAchievementsCheckTask;
	}
}
