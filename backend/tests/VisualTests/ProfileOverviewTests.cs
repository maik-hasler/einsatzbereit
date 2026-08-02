using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ProfileOverviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfilePage_ShowsSingleColumnStructure_WhenAuthenticated()
	{
		// #794: /profile was consolidated from a Profile/Activity tab switcher
		// into a single cohesive page - no tab bar, all content on one page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile Details" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Sign-ups" }))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Profile", Exact = true }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Activity", Exact = true }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task ProfilePage_ShowsHomeBreadcrumb()
	{
		// #590: /profile never called usePageToolbar, so it rendered with no
		// breadcrumb trail at all, unlike every other authenticated page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Should land on /profile?tab=engagements
		await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/profile\?tab=engagements"));

		// The page heading (h1) must still be visible after redirect
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task ProfileAchievementsTabDeepLink_ScrollsToBadgesSection()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		// #794: there's no more Achievements tab to switch to - the legacy
		// ?tab=achievements deep link (formerly reached via the now-removed
		// /achievements redirect, #843) scrolls the single-page profile to
		// the Badges section instead.
		await Page.GotoAsync($"{origin}/profile?tab=achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var badgesHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" });
		await Expect(badgesHeading).ToBeVisibleAsync(new() { Timeout = 20_000 });

		// A fixed "bounding box Y < 300" threshold (the previous version of this
		// assertion, #1515) is inherently flaky: scrollIntoView({block: "start"})
		// aligns the #achievements section's top with the viewport's top only
		// when the document has enough scroll room left below it to do so - on
		// a page just barely short of that (this one, depending on exactly how
		// much content renders above/below Badges), the browser clamps to its
		// max scroll offset instead, leaving the section a few px shy of 0 for
		// entirely legitimate layout reasons, not a still-running animation. A
		// hardcoded pixel threshold can't tell "still scrolling" apart from
		// "already as far as the page can physically go" - so assert against
		// the actual achievable scroll position instead: the current scrollTop
		// should match the section's absolute document offset, clamped to the
		// document's max scroll. Still polled, since the effect's own
		// requestAnimationFrame can land a frame after NetworkIdle.
		double? scrollTop = null;
		double? desiredScrollTop = null;
		await PollUntilAsync(async () =>
		{
			var position = await Page.EvaluateAsync<double[]>(
				"""
				() => {
					const el = document.getElementById('achievements');
					const scrollingEl = document.scrollingElement;
					const absoluteTop = el.getBoundingClientRect().top + window.scrollY;
					const maxScrollTop = scrollingEl.scrollHeight - scrollingEl.clientHeight;
					return [scrollingEl.scrollTop, Math.min(absoluteTop, maxScrollTop)];
				}
				""");
			scrollTop = position[0];
			desiredScrollTop = position[1];
			return Math.Abs(scrollTop.Value - desiredScrollTop.Value) < 2;
		}, () => "the page should have scrolled the Badges section as close to the top as the "
			+ $"document allows (scrollTop = {scrollTop}, desired = {desiredScrollTop})");
	}

	[Test]
	public async Task ProfilePage_RendersAchievementsAboveAccountActionCards()
	{
		// #706: achievements used to render last on the Profile tab, below the
		// "Create organization" and "Danger zone" cards, in a full-width block
		// that visually broke away from the narrower profile column above it.
		// They should now render above the account-action card. (The
		// "Organizations" card itself was later removed entirely - org
		// management now only happens via the /app entry point.)
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var badgesHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" });
		var dangerZoneHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Danger zone" });

		await Expect(badgesHeading).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(dangerZoneHeading).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var badgesBox = await badgesHeading.BoundingBoxAsync();
		var dangerZoneBox = await dangerZoneHeading.BoundingBoxAsync();

		badgesBox.Should().NotBeNull();
		dangerZoneBox.Should().NotBeNull();

		badgesBox!.Y.Should().BeLessThan(dangerZoneBox!.Y, "achievements must render above the account cards");
	}

	[Test]
	public async Task PublicUserProfile_ShowsBioSkillsLanguagesAndPreferredContact()
	{
		// #576: bio/skills/languages/preferredContact were captured on the owner's
		// own profile but never exposed on the public /users/{userId} page. Set
		// them on vera's own profile, then verify they appear on her public page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
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

		// Exact: true - substring matching would also hit NotificationPreferencesSection's
		// "Save preferences" button, elsewhere on this same page.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// This page's data comes from a fresh fetch that fans out to Keycloak (user
		// lookup) plus several DB queries (engagement count, badges, profile), so it
		// is slower than a typical page load - give it extra headroom (30s, up from
		// 20s) beyond what's used elsewhere in this file for API-heavy pages, since
		// this specific fetch fans out further than most.
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.GetByText(skill)).ToBeVisibleAsync();
		await Expect(Page.GetByText("Preferred contact channel")).ToBeVisibleAsync();

		// Regression for #766: this bio/skills/contact wrapper had `mx-auto`,
		// centering it independently of the left-aligned avatar/name row
		// above it - a dead column on wide viewports.
		await AssertMaxWidthContentLeftAlignedAsync("Public user profile page");
	}

	[Test]
	public async Task ProfileEditForm_ShowsUsernameAndEmailFields()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");

		// Edit is a single quick-action button in the header toolbar (#794) -
		// .First is a harmless no-op.
		var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
		await Expect(editButton).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await editButton.ClickAsync();

		await Expect(Page.GetByLabel("Username")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByLabel("Email address")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Test]
	public async Task ProfileEditForm_DisplaysUsername_AfterLogin()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");

		var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
		await Expect(editButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await editButton.ClickAsync();

		await Expect(Page.GetByLabel("Username")).ToHaveValueAsync("vera",
			new() { Timeout = 10_000 });
	}

	[Test]
	public async Task ProfileEditForm_SavesProfileChanges()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/profile");

		var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
		await Expect(editButton).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await editButton.ClickAsync();

		await Page.GetByLabel("First name").FillAsync("Vera", new() { Timeout = 10_000 });
		await Page.GetByLabel("Last name").FillAsync("Sample");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

		await Expect(Page.GetByText("Profile saved.")).ToBeVisibleAsync();
	}

	[Test]
	public async Task ProfileEditForm_PreservesUnsavedDraft_ThroughSilentTokenRenewal()
	{
		// #1221: react-oidc-context's automaticSilentRenew mints a fresh access
		// token roughly every ~4 minutes in production (Keycloak's ~5 min default
		// access token lifespan minus oidc-client-ts's 60s renewal buffer). The
		// profile-load effect used to depend on that token's identity, so every
		// renewal re-triggered form.reset(profile) and wiped whatever the user
		// was mid-typing.
		//
		// FastSignInAsync can't reproduce this - its ROPC-minted refresh token
		// belongs to the "frontend-test" client, not "frontend" (see its own
		// comment on why it drops the refresh token entirely rather than seed an
		// unusable one), so a silent renewal attempt would fail outright. A real
		// LoginAsync session's refresh token is valid for "frontend", so
		// oidc-client-ts's automaticSilentRenew can genuinely succeed via a
		// background refresh-token grant (UserManager.signinSilent prefers the
		// refresh token over the interactive iframe whenever one is present - no
		// Keycloak SSO session needed for that path). Falsifying the just-minted
		// session's stored expires_at makes that renewal fire on a short,
		// predictable schedule instead of waiting out the realm's local
		// accessTokenLifespan (3600s, bumped in AppHost.cs for other tests'
		// benefit) or production's real ~4 minutes.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		// 80s remaining life at the next mount (below) means oidc-client-ts's
		// AccessTokenEvents arms the "expiring" timer for 20s after that mount
		// (80s - its 60s notification buffer) - comfortably after the
		// navigate/edit/type steps below, but short enough not to bloat this
		// test's runtime.
		var originalAccessToken = await Page.EvaluateAsync<string>(
			"""
			() => {
				for (let i = 0; i < sessionStorage.length; i++) {
					const key = sessionStorage.key(i);
					if (key && key.startsWith('oidc.user:')) {
						const entry = JSON.parse(sessionStorage.getItem(key));
						entry.expires_at = Math.floor(Date.now() / 1000) + 80;
						sessionStorage.setItem(key, JSON.stringify(entry));
						return entry.access_token;
					}
				}
				throw new Error('no oidc.user storage entry found after LoginAsync');
			}
			""");

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
		await Expect(editButton).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await editButton.ClickAsync();

		var draftBio = $"Unsaved silent-renewal draft {Guid.NewGuid()}";
		await Page.Locator("#bio").FillAsync(draftBio, new() { Timeout = 10_000 });

		// Confirms the renewal actually happened - without this, the assertion
		// below would trivially pass on the old, buggy dependency array too,
		// simply because nothing occurred within the wait.
		string? renewedAccessToken = null;
		await PollUntilAsync(async () =>
		{
			renewedAccessToken = await Page.EvaluateAsync<string?>(
				"""
				() => {
					for (let i = 0; i < sessionStorage.length; i++) {
						const key = sessionStorage.key(i);
						if (key && key.startsWith('oidc.user:')) {
							return JSON.parse(sessionStorage.getItem(key)).access_token;
						}
					}
					return null;
				}
				""");
			return renewedAccessToken != null && renewedAccessToken != originalAccessToken;
		}, () => "silent token renewal should have replaced the stored access token "
			+ $"within the timeout (last observed: {renewedAccessToken ?? "null"})",
			timeoutMs: 40_000);

		await Expect(Page.Locator("#bio")).ToHaveValueAsync(draftBio);
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeVisibleAsync();
	}
}
