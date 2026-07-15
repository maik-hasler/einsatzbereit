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
	/// (e.g. olaf, who already has confirmed-engagement achievements from seed
	/// data) re-announced every existing achievement as newly unlocked.
	/// </summary>
	[Test]
	public async Task ExistingAchievements_DoNotReToastAsNew_OnFreshBrowserContext()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
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
			"olaf must already have at least one achievement for this regression test to be meaningful");

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

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
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

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
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

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/achievements");

		var shareBtn2 = Page.GetByRole(AriaRole.Button, new() { Name = "Share achievements" });
		await Expect(shareBtn2).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await shareBtn2.ClickAsync();

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();

		// Click the backdrop (top-left corner, outside the dialog box)
		await Page.Mouse.ClickAsync(5, 5);

		await Expect(Page.Locator("[role=\"dialog\"]")).ToBeHiddenAsync();
	}
}
