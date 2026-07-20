using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AchievementsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	/// <summary>
	/// Regression for #645: useAchievementNotifier only seeded the "seen"
	/// localStorage set when the account had zero achievements, so a fresh
	/// browser/device/profile for an account that already has achievements
	/// re-announced every existing achievement as newly unlocked.
	/// </summary>
	[Test]
	public async Task ExistingAchievements_DoNotReToastAsNew_OnFreshBrowserContext()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");

		// Deterministically guarantee olaf already has an achievement before
		// this test's fresh browser context ever loads the page - relying on
		// some OTHER VisualTests class having incidentally earned him one
		// does not hold: every other test uses olaf only as the *organizer*
		// confirming other users' engagements, and milestone achievements are
		// awarded to the volunteer, never the organizer (see
		// ConfirmEngagementCommandHandler) - so nothing else in this suite
		// ever earns olaf a badge, seed data included (seed only makes him
		// an organizer, never a confirmed volunteer). Olaf applies to his
		// own opportunity and confirms it himself here - nothing in
		// CreateEngagementCommandHandler/ConfirmEngagementCommandHandler
		// blocks organizer == volunteer. Must happen BEFORE FastSignInAsync
		// below: the whole point of this test is the notifier's very first,
		// on-mount check of already-existing achievements, so the badge has
		// to exist before that mount, not be granted while the page is open.
		var suffix = Guid.NewGuid().ToString("N");
		using (var seedHttp = new HttpClient { BaseAddress = backend })
		{
			seedHttp.DefaultRequestHeaders.Add(
				"Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");

			var orgResponse = await seedHttp.PostAsJsonAsync(
				"/v1/organizations", new { name = $"AchievementsSelfSeed Org {suffix}" });
			orgResponse.EnsureSuccessStatusCode();
			var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
			var organizationId = org.GetProperty("id").GetProperty("value").GetString();

			var oppResponse = await seedHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				title = $"AchievementsSelfSeed Opportunity {suffix}",
				description = "Created by AchievementsTests to guarantee olaf has an achievement.",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "IndividualContact",
				checkInMethod = "None",
				isDraft = false,
			});
			oppResponse.EnsureSuccessStatusCode();
			var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
			var opportunityId = opportunity.GetProperty("id").GetString();

			var engagementResponse = await seedHttp.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/engagements",
				new { message = "Applying via AchievementsTests to seed a real achievement." });
			engagementResponse.EnsureSuccessStatusCode();
			var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
			var engagementId = engagement.GetProperty("id").GetString();

			(await seedHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
				.EnsureSuccessStatusCode();
		}

		// Deterministically guarantee olaf has at least one achievement before
		// he ever logs in below, instead of relying on some other VisualTests
		// class having already confirmed an engagement for him. AspireFixture
		// is shared (Shared = SharedType.PerTestSession) across every test
		// class with no ordering guarantee between them, so that assumption
		// was a race: if this test happened to run before whichever class
		// first grants olaf his "first-step" badge, GET /v1/me/achievements
		// legitimately came back empty. Achievement rows are never deleted
		// once granted, so seeding one here (following the same
		// create-org/create-opportunity/publish/apply/confirm flow as
		// EngagementCalendarTests) is safe regardless of what other tests do
		// concurrently.
		var setupSession = await Fixture.SignInAsync("olaf", "olaf123");
		using (var setupHttp = new HttpClient { BaseAddress = backend })
		{
			setupHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {setupSession.AccessToken}");

			var suffix = Guid.NewGuid().ToString("N");
			var orgResponse = await setupHttp.PostAsJsonAsync(
				"/v1/organizations", new { name = $"AchievementSeed Org {suffix}" });
			orgResponse.EnsureSuccessStatusCode();
			var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
			var organizationId = org.GetProperty("id").GetProperty("value").GetString();

			var oppResponse = await setupHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				title = $"AchievementSeed Opportunity {suffix}",
				description = "Created by AchievementsTests to guarantee a confirmed engagement",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "Waitlist",
				checkInMethod = "None",
				isDraft = true,
			});
			oppResponse.EnsureSuccessStatusCode();
			var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
			var opportunityId = opportunity.GetProperty("id").GetString();

			var start = DateTimeOffset.UtcNow.AddDays(3);
			var end = start.AddHours(2);
			var slotResponse = await setupHttp.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
				new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 });
			slotResponse.EnsureSuccessStatusCode();
			var slots = await slotResponse.Content.ReadFromJsonAsync<JsonElement>();
			var timeSlotId = slots[0].GetProperty("id").GetString();

			(await setupHttp.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
				.EnsureSuccessStatusCode();

			var engagementResponse = await setupHttp.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/engagements",
				new { type = "Waitlist", timeSlotId, message = (string?)null });
			engagementResponse.EnsureSuccessStatusCode();
			var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
			var engagementId = engagement.GetProperty("id").GetString();

			(await setupHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", content: null))
				.EnsureSuccessStatusCode();
		}

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in localStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		var achievementsResponse = await http.GetAsync("/v1/me/achievements");
		achievementsResponse.EnsureSuccessStatusCode();
		var achievements = await achievementsResponse.Content.ReadFromJsonAsync<JsonElement>();
		achievements.GetArrayLength().Should().BeGreaterThan(0,
			"the confirmed engagement set up above must have granted olaf at least one achievement " +
			"for this regression test to be meaningful");

		// Each VisualTests test gets a fresh, isolated browser context (see
		// VisualTestBase) - no einsatzbereit:seen-achievements localStorage entry
		// yet, simulating a new device/browser/profile. The notifier's first
		// check fires on mount; give it a moment, then assert no "New badge
		// unlocked" toast appeared for an already-earned badge.
		await Page.WaitForTimeoutAsync(3000);

		var badgeToast = Page.Locator("[role='alert']", new() { HasText = "New badge unlocked" });
		(await badgeToast.CountAsync()).Should().Be(0,
			"an already-earned achievement must not re-announce itself as newly unlocked on a fresh browser context");
	}

	[Test]
	public async Task ShareButton_OpensModal_WithQrCodeAndCopyLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");

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

		var shareBtn = Page.GetByRole(AriaRole.Button,
			new() { Name = "Share achievements" });
		await Expect(shareBtn).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await shareBtn.ClickAsync();

		var dialog = Page.Locator("[role=\"dialog\"]");
		await Expect(dialog).ToBeVisibleAsync();

		// QR code SVG is rendered inside the dialog
		await Expect(dialog.Locator("svg").First).ToBeVisibleAsync();

		// #695: share URL now points at the combined public profile
		// (/users/:userId), not the achievements-only deep link.
		var dialogText = await dialog.TextContentAsync();
		await Expect(dialog.GetByRole(AriaRole.Button,
			new() { Name = "Copy link" }))
			.ToBeVisibleAsync();
		dialogText.Should().Contain($"/users/{userId}");
		dialogText.Should().NotContain("/achievements");
	}

	[Test]
	public async Task ShareModal_ClosesOnEscape()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");

		var shareBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" });
		await Expect(shareBtn).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await shareBtn.ClickAsync();

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();

		await Page.Keyboard.PressAsync("Escape");

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeHiddenAsync();
	}

	[Test]
	public async Task ShareModal_ClosesOnBackdropClick()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");

		var shareBtn2 = Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" });
		await Expect(shareBtn2).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await shareBtn2.ClickAsync();

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();

		// Click the backdrop (top-left corner, outside the dialog box)
		await Page.Mouse.ClickAsync(5, 5);

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeHiddenAsync();
	}

	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}
}
