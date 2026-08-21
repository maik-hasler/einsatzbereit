using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ProfileOverviewTests(AspireFixture fixture) : VisualTestBase(fixture)
{
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
	public async Task PublicUserProfile_ShowsBioSkillsAndLanguagesButNotPreferredContact()
	{
		// #576: bio/skills/languages were captured on the owner's own profile but
		// never exposed on the public /users/{userId} page. Set them on vera's own
		// profile, then verify they appear on her public page.
		//
		// #1028: PreferredContact (and Phone) are deliberately excluded from this
		// page - it's reachable by anonymous visitors, and showing a contact
		// preference with no way to actually reach the volunteer was decorative/
		// misleading. Contact info only ever reaches an organizer through an
		// actual engagement, not a cold view of the public profile - so this test
		// now asserts the opposite of what #576 asserted.
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

		// #2066: the header "Edit" button only renders once the profile has
		// bio/skills/languages already filled in - vera's profile starts empty
		// in this shared session, so the reachable control is the empty-state
		// CTA instead, which carries the same "profile-edit" testid.
		await Page.GetByTestId("profile-edit").ClickAsync();

		var bioText = $"Public profile smoke test bio {Guid.NewGuid()}";
		await Page.Locator("#bio").FillAsync(bioText);

		var skill = $"Smoke576-{Guid.NewGuid():N}".Substring(0, 16);
		await Page.Locator("#skill-input").FillAsync(skill);
		await Page.Locator("#skill-input").PressAsync("Enter");

		await Page.Locator("#preferred-contact").ClickAsync();
		await Page.GetByRole(AriaRole.Option, new() { Name = "Email" }).ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Regression for #1112: ProfileFieldsView rendered sibling field blocks
		// with no spacing of its own, relying on the caller to supply a
		// `space-y-*` wrapper - ProfileOverviewPage's non-editing view didn't,
		// so Bio/Skills/Languages/Preferred contact stacked flush against each
		// other. The component now owns its own spacing.
		await AssertVerticalGapBetweenAsync(
			Page.GetByText("Bio", new() { Exact = true }),
			Page.GetByText("Skills & interests", new() { Exact = true }),
			"Profile overview page (#1112)");

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// This page's data comes from a fresh fetch that fans out to Keycloak (user
		// lookup) plus several DB queries (engagement count, badges, profile), so it
		// is slower than a typical page load - give it extra headroom (30s, up from
		// 20s) beyond what's used elsewhere in this file for API-heavy pages, since
		// this specific fetch fans out further than most.
		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.GetByText(skill)).ToBeVisibleAsync();
		await Expect(Page.GetByText("Preferred contact channel")).Not.ToBeVisibleAsync();

		// Regression for #1112: this page's wrapper already supplied `space-y-5`
		// before the fix, so it never showed the bug - kept here as a guard that
		// moving spacing into ProfileFieldsView didn't remove it from this page.
		await AssertVerticalGapBetweenAsync(
			Page.GetByText("Bio", new() { Exact = true }),
			Page.GetByText("Skills & interests", new() { Exact = true }),
			"Public user profile page (#1112)");

		// Regression for #766: this bio/skills/contact wrapper had `mx-auto`,
		// centering it independently of the left-aligned avatar/name row
		// above it - a dead column on wide viewports.
		await AssertMaxWidthContentLeftAlignedAsync("Public user profile page");
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

		var editButton = Page.GetByTestId("profile-edit");
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

	// einsatzbereit#1069: seeds a checked-in engagement with feedback already
	// submitted (via raw HTTP) so the Edit/Delete tests below don't have to
	// drive the initial "Leave feedback" submission through the UI first.
	private async Task<string> SeedCheckedInEngagementWithFeedbackAsync(int rating, string comment)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"FeedbackEdit Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");

		var oppResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/volunteer-opportunities", new
		{
			titleDe = $"FeedbackEdit Opportunity {suffix}",
			descriptionDe = "Created by ProfileOverviewTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "Manual",
			isDraft = false,
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()
			?? throw new InvalidOperationException("Created opportunity had no id.");

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await PostJsonWithRetryAsync(veraHttp,
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "FeedbackEdit application." });
		applyResponse.EnsureSuccessStatusCode();
		var engagement = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()
			?? throw new InvalidOperationException("Created engagement had no id.");

		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null)).EnsureSuccessStatusCode();
		(await olafHttp.PostAsync($"/v1/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();
		(await veraHttp.PostAsJsonAsync($"/v1/engagements/{engagementId}/feedback", new { rating, comment }))
			.EnsureSuccessStatusCode();

		return engagementId;
	}

	[Test]
	public async Task ActivitySection_EditFeedback_PersistsUpdatedRatingAndComment()
	{
		var originalComment = $"Original comment {Guid.NewGuid():N}";
		var engagementId = await SeedCheckedInEngagementWithFeedbackAsync(3, originalComment);
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// #1684: ActivitySection (and this data-testid) moved from /profile to
		// its own page at /my-signups.
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// einsatzbereit#675: a checked-in Confirmed engagement is classified as
		// Past, not "Current & upcoming".
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(card.GetByText("Feedback given")).ToBeVisibleAsync();

		await card.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "Edit your feedback" })).ToBeVisibleAsync();
		await Expect(dialog.Locator("#feedback-comment")).ToHaveValueAsync(originalComment);

		var updatedComment = $"Updated comment {Guid.NewGuid():N}";
		await dialog.Locator("#feedback-comment").FillAsync(updatedComment);
		await dialog.GetByRole(AriaRole.Button, new() { Name = "5 stars" }).ClickAsync();
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		// Reload so the page re-fetches from the server, proving the edit
		// actually persisted rather than only updating local UI state.
		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var reloadedCard = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(reloadedCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await reloadedCard.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
		await Expect(Page.Locator("#feedback-comment")).ToHaveValueAsync(updatedComment);
	}

	[Test]
	public async Task ActivitySection_DeleteFeedback_ReturnsEngagementToLeaveFeedbackState()
	{
		var engagementId = await SeedCheckedInEngagementWithFeedbackAsync(4, "To be deleted");
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		// #1684: ActivitySection (and this data-testid) moved from /profile to
		// its own page at /my-signups.
		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var card = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await card.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
		var confirmDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(confirmDialog).ToBeVisibleAsync();
		await Expect(confirmDialog.GetByRole(AriaRole.Heading, new() { Name = "Delete feedback?" })).ToBeVisibleAsync();
		await confirmDialog.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" }).ClickAsync();
		await Expect(confirmDialog).Not.ToBeVisibleAsync();

		await Expect(card.GetByText("Feedback given")).Not.ToBeVisibleAsync();
		await Expect(card.GetByRole(AriaRole.Button, new() { Name = "Leave feedback" })).ToBeVisibleAsync();

		// Reload to prove the delete persisted server-side, not just locally.
		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var reloadedCard = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(reloadedCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(reloadedCard.GetByRole(AriaRole.Button, new() { Name = "Leave feedback" })).ToBeVisibleAsync();
	}
}
