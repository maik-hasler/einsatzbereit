using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ProfileOverviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfilePage_ShowsTwoTabButtons_WhenAuthenticated()
	{
		// Regression: /profile should render a tab bar instead of a flat page.
		// Introduced by PR #508 (RC.150 - unified profile overview), consolidated
		// from four tabs to two (Profile+Achievements, Activity) by #695.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Profile" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Activity" }))
			.ToBeVisibleAsync(new() { Timeout = 5_000 });

		// Achievements now live on the Profile tab itself, not a separate tab.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}

	[Test]
	public async Task ProfilePage_ShowsHomeBreadcrumb()
	{
		// #590: /profile never called usePageToolbar, so it rendered with no
		// breadcrumb trail at all, unlike every other authenticated page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var breadcrumb = Page.Locator("nav[aria-label='Breadcrumb']");
		await Expect(breadcrumb).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(breadcrumb.Locator("a[href='/']")).ToBeVisibleAsync();
		await Expect(breadcrumb.GetByText("Profile", new() { Exact = true })).ToBeVisibleAsync();
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

	[Test]
	public async Task ProfilePage_RendersAchievementsAboveAccountActionCards()
	{
		// #706: achievements used to render last on the Profile tab, below the
		// "Create organization" and "Danger zone" cards, in a full-width block
		// that visually broke away from the narrower profile column above it.
		// They should now render above both account-action cards.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var badgesHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" });
		var organizationsHeading = Page.GetByRole(
			AriaRole.Heading,
			new() { Name = "Organizations" }
		);
		var dangerZoneHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Danger zone" });

		await Expect(badgesHeading).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(organizationsHeading).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(dangerZoneHeading).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var badgesBox = await badgesHeading.BoundingBoxAsync();
		var organizationsBox = await organizationsHeading.BoundingBoxAsync();
		var dangerZoneBox = await dangerZoneHeading.BoundingBoxAsync();

		badgesBox.Should().NotBeNull();
		organizationsBox.Should().NotBeNull();
		dangerZoneBox.Should().NotBeNull();

		badgesBox!.Y.Should().BeLessThan(organizationsBox!.Y, "achievements must render above the account cards");
		badgesBox.Y.Should().BeLessThan(dangerZoneBox!.Y, "achievements must render above the account cards");
	}

	[Test]
	public async Task PublicUserProfile_ShowsBioSkillsLanguagesAndPreferredContact()
	{
		// #576: bio/skills/languages/preferredContact were captured on the owner's
		// own profile but never exposed on the public /users/{userId} page. Set
		// them on vera's own profile, then verify they appear on her public page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");
		userId.Should().NotBeNull("the logged-in user's id must be available via the OIDC profile claims");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First.ClickAsync();

		var bioText = $"Public profile smoke test bio {Guid.NewGuid()}";
		await Page.Locator("#bio").FillAsync(bioText);

		var skill = $"Smoke576-{Guid.NewGuid():N}".Substring(0, 16);
		await Page.Locator("#skill-input").FillAsync(skill);
		await Page.Locator("#skill-input").PressAsync("Enter");

		await Page.Locator("#preferred-contact").ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Email" }).ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// This page's data comes from a fresh fetch that fans out to Keycloak (user
		// lookup) plus several DB queries (engagement count, badges, profile), so it
		// is slower than a typical page load - give it the same headroom already
		// used elsewhere in this file for API-heavy pages rather than the default.
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByText(skill)).ToBeVisibleAsync();
		await Expect(Page.GetByText("Preferred contact channel")).ToBeVisibleAsync();
	}
}
