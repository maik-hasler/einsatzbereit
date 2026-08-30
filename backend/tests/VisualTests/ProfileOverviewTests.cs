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

		await Page.GotoAsync($"{origin}/profile?tab=achievements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var badgesHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Badges" });
		await Expect(badgesHeading).ToBeVisibleAsync(new() { Timeout = 20_000 });

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

		await AssertVerticalGapBetweenAsync(
			Page.GetByText("Bio", new() { Exact = true }),
			Page.GetByText("Skills & interests", new() { Exact = true }),
			"Profile overview page (#1112)");

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText(bioText)).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page.GetByText(skill)).ToBeVisibleAsync();
		await Expect(Page.GetByText("Preferred contact channel")).Not.ToBeVisibleAsync();

		await AssertVerticalGapBetweenAsync(
			Page.GetByText("Bio", new() { Exact = true }),
			Page.GetByText("Skills & interests", new() { Exact = true }),
			"Public user profile page (#1112)");

		// Centered, not left-aligned: this page's own h1 sits at the site's
		// standard content gutter because PageHeaderBand centers a max-w-5xl
		// block, so a flush-left body was 176px out of alignment with its own
		// title and stopped 176px short of the right gutter (#2330). /help and
		// /profile already center theirs; this was the odd one out.
		await AssertMaxWidthContentCenteredAsync("Public user profile page");
	}

	[Test]
	public async Task ProfileEditForm_PreservesUnsavedDraft_ThroughSilentTokenRenewal()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

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
		(await olafHttp.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/engagements/{engagementId}/check-in", null)).EnsureSuccessStatusCode();
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

		await Page.GotoAsync($"{origin}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

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
		await dialog.GetByRole(AriaRole.Radio, new() { Name = "5 stars" }).ClickAsync();
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

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

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("engagements-scope-past").ClickAsync();

		var reloadedCard = Page.Locator($"[data-engagement-id='{engagementId}']");
		await Expect(reloadedCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(reloadedCard.GetByRole(AriaRole.Button, new() { Name = "Leave feedback" })).ToBeVisibleAsync();
	}
}
