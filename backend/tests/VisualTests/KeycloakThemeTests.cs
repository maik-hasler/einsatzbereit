using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Covers the custom Keycloak login theme (<c>keycloak/themes/einsatzbereit</c>).
///
/// It had no automated coverage of any kind, and the gap showed: the theme
/// overrode four templates and inherited the rest from <c>parent=base</c>, so
/// the pages a real signup walks through - confirm your email, then set a
/// password - rendered Keycloak's stock markup inside the theme's card, with
/// its own floating-label rules unable to match it. Nothing in CI could see
/// that, because nothing in CI ever loaded those pages.
///
/// These tests drive Keycloak's own origin directly rather than going through
/// the SPA (<see cref="AuthHelper.LoginAsync"/>), because most of these pages
/// are only reachable from a required action, and the point here is the page
/// itself rather than how the app got you there.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class KeycloakThemeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string Realm = "einsatzbereit";
	private const string FrontendClientId = "frontend";

	// Mirrors theme.properties' siteUrl. Declared there rather than derived,
	// because the frontend client carries no baseUrl - see keycloak/AGENTS.md.
	private const string SiteUrl = "https://einsatzbereit.maik-hasler.de";

	// Satisfies the realm's passwordPolicy (upperCase(1), length(8)) - a
	// weaker one is rejected by the admin API at user-creation time, not at
	// login, so it would fail in CreateThrowawayUserAsync with an error that
	// says nothing about this test.
	private const string ThrowawayPassword = "Throwaway123";

	/// <summary>
	/// Builds an authorization URL for the real <c>frontend</c> client. PKCE is
	/// mandatory on it (<c>pkceCodeChallengeMethod: S256</c>), so a request
	/// without a challenge never reaches the login page at all - it comes back
	/// as an error page, which would silently turn every assertion below into a
	/// test of error.ftl.
	/// </summary>
	private string AuthUrl(string endpoint = "auth", string? locale = null)
	{
		var keycloak = Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/');
		var frontend = Fixture.GetEndpoint("frontend").ToString().TrimEnd('/');

		var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
		var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

		var query = new Dictionary<string, string>
		{
			["client_id"] = FrontendClientId,
			["redirect_uri"] = frontend,
			["response_type"] = "code",
			["scope"] = "openid",
			["state"] = "theme-test",
			["code_challenge"] = challenge,
			["code_challenge_method"] = "S256",
		};
		if (locale is not null)
			query["ui_locales"] = locale;

		var q = string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
		return $"{keycloak}/realms/{Realm}/protocol/openid-connect/{endpoint}?{q}";
	}

	private static string Base64Url(byte[] bytes) =>
		Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

	/// <summary>
	/// Asserts the shell every page in this theme shares: the brand lockup, the
	/// card, and the way back to the product.
	/// </summary>
	private async Task AssertThemeShellAsync(string label)
	{
		await Expect(Page.Locator(".auth-card")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator(".auth-logo")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();

		// The stylesheet actually applied, rather than a 404 leaving the page as
		// unstyled default-serif markup that would still satisfy every locator
		// above. bg-white on the card is the cheapest single proof of that.
		var cardBackground = await Page.Locator(".auth-card")
			.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
		cardBackground.Should().Be("rgb(255, 255, 255)",
			$"{label}: the theme stylesheet should have loaded and styled the card");

		// The back link sits *below* the card, not beside it. .auth-main is a
		// flex container holding both, and it was missing flex-direction: column
		// - so the two laid out as a row on every page in the theme, parking the
		// link in the whitespace to the right of the card. At 390px wide it took
		// a third of the viewport with it and squeezed the login form's
		// "remember me" and "forgot password" onto two lines each.
		await AssertVerticalGapBetweenAsync(
			Page.Locator(".auth-card"), Page.Locator(".auth-back"), label);
	}

	[Test]
	public async Task Login_RendersThemeShellWithBackLinkBelowCard()
	{
		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		await AssertThemeShellAsync("login");
		await Expect(Page.Locator(".card-eyebrow")).ToHaveTextAsync("Sign in");
		await Expect(Page.Locator("#password")).ToBeVisibleAsync();
		await Expect(Page.Locator("#kc-login")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Login_PrimaryButton_UsesTheProductsOwnGreen()
	{
		// Button.tsx documents that white text on brand-600 (#2d8a5e) measures
		// ~4.3:1, under the WCAG AA 4.5:1 floor this suite's own axe scans
		// enforce, and that every primary button in the app therefore uses
		// brand-700 (#226947). The auth pages were the one surface still on
		// brand-600 - both a contrast failure and a visibly different green
		// from the button the same person clicked one page earlier.
		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#kc-login")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		var background = await Page.Locator("#kc-login")
			.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
		background.Should().Be("rgb(34, 105, 71)", "the primary button should use brand-700");
	}

	[Test]
	public async Task Register_OmitsPasswordFields_AndSaysWhy()
	{
		// The realm has verifyEmail on, so Keycloak deliberately leaves the
		// password off this form and collects it after the address is confirmed
		// (RegistrationPassword.buildPage). Nothing said so, and the form just
		// looked like it had lost a field. If verifyEmail is ever turned off,
		// this test failing is the correct outcome - register.ftl renders the
		// password fields again and the lead must go with it.
		await Page.GotoAsync(AuthUrl("registrations", locale: "en"));
		await Expect(Page.Locator("#email")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		await AssertThemeShellAsync("register");
		await Expect(Page.Locator("#username")).ToBeVisibleAsync();
		await Expect(Page.Locator("#password")).ToHaveCountAsync(0);
		await Expect(Page.Locator(".card-lead"))
			.ToContainTextAsync("set your password right after");
	}

	[Test]
	public async Task ResetPassword_IsThemed()
	{
		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		await Page.GetByRole(AriaRole.Link, new() { Name = "Forgot Password?" }).ClickAsync();
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await AssertThemeShellAsync("reset-password");
		await Expect(Page.Locator(".card-eyebrow")).ToHaveTextAsync("Account recovery");
	}

	[Test]
	public async Task VerifyEmail_IsThemed()
	{
		var username = $"theme-verify-{Guid.NewGuid():N}"[..24];
		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: false, requiredActions: []);
		try
		{
			await SignInThroughKeycloakUiAsync(username);

			await AssertThemeShellAsync("verify-email");
			await Expect(Page.Locator(".card-eyebrow")).ToHaveTextAsync("Confirm your email");
			// .instruction is Keycloak's own class for this page's body copy. It
			// had no rule in the theme at all, so the single sentence this page
			// exists to deliver rendered unstyled.
			await Expect(Page.Locator(".instruction")).ToBeVisibleAsync();
			var color = await Page.Locator(".instruction").First
				.EvaluateAsync<string>("el => getComputedStyle(el).color");
			color.Should().NotBe("rgb(0, 0, 0)",
				"verify-email: .instruction should pick up the theme's prose styling, not browser defaults");
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}

	[Test]
	public async Task UpdatePassword_FloatingLabelsAttachToTheirField()
	{
		// The single most-visited page in this theme after sign-in itself, and
		// the one that was worst off: base's markup puts the <label> in a
		// wrapper div *above* the input rather than as its sibling, which the
		// theme's floating-label rules (.form-input + .form-label) cannot match.
		// The label is position: absolute, so with no positioned ancestor and no
		// rule to place it, it escaped its field entirely.
		var username = $"theme-pw-{Guid.NewGuid():N}"[..24];
		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: ["UPDATE_PASSWORD"]);
		try
		{
			await SignInThroughKeycloakUiAsync(username);

			await Expect(Page.Locator("#password-new")).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await AssertThemeShellAsync("update-password");
			await Expect(Page.Locator(".card-eyebrow")).ToHaveTextAsync("Password");
			await Expect(Page.Locator("#password-confirm")).ToBeVisibleAsync();

			// The label overlaps its own input, which is only true when the two
			// are siblings inside the positioned .form-field wrapper.
			var labelSitsOnField = await Page.EvaluateAsync<bool>(
				"""
				() => {
					const input = document.getElementById('password-new');
					const label = document.querySelector('label[for="password-new"]');
					if (!input || !label) return false;
					const i = input.getBoundingClientRect();
					const l = label.getBoundingClientRect();
					return l.left >= i.left && l.right <= i.right
						&& l.top >= i.top && l.bottom <= i.bottom;
				}
				""");
			labelSitsOnField.Should().BeTrue(
				"update-password: the floating label should sit inside its own field, "
				+ "which only holds when the template emits label as a sibling of the input");

			// The visibility toggle is wired up here too - it is a separate
			// script from the floating labels and had its own latent bug
			// (a DOMContentLoaded-only listener with no readyState guard).
			await Page.Locator("button[aria-controls='password-new']").ClickAsync();
			await Expect(Page.Locator("#password-new")).ToHaveAttributeAsync("type", "text");
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}

	[Test]
	public async Task UpdateProfile_IsThemed()
	{
		// This page's fields come from base's userProfileCommons macro, which
		// renders labels above their inputs rather than as siblings - so unlike
		// every fixed form in this theme it deliberately falls to the static
		// label treatment. Asserting it here keeps that fallback honest: a
		// label that cannot float should look like a plain label, not like one
		// stranded mid-field.
		var username = $"theme-profile-{Guid.NewGuid():N}"[..24];
		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: ["UPDATE_PROFILE"]);
		try
		{
			await SignInThroughKeycloakUiAsync(username);

			await Expect(Page.Locator("#kc-update-profile-form")).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await AssertThemeShellAsync("update-profile");

			var labelIsStatic = await Page.EvaluateAsync<bool>(
				"""
				() => {
					const label = document.querySelector('.form-label-wrapper .form-label');
					return !!label && getComputedStyle(label).position === 'static';
				}
				""");
			labelIsStatic.Should().BeTrue(
				"update-profile: labels rendered above their input should be statically positioned");
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}

	[Test]
	public async Task ErrorPage_OffersAVisibleWayBack()
	{
		// Two separate defects met on this page. Base only renders a way out
		// when ${client.baseUrl} is set, and this realm's frontend client has
		// none - so the page a stuck visitor is most likely to be looking at
		// had nothing to click. And once the theme added a button, the prose
		// rule for links inside #kc-error-message (id, 1-0-1) outranked
		// .btn-primary (0-1-0) and painted the label brand-700 on a brand-700
		// fill: a solid green bar with an invisible label.
		var keycloak = Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/');
		await Page.GotoAsync(
			$"{keycloak}/realms/{Realm}/login-actions/action-token?key=not-a-real-key&client_id={FrontendClientId}");

		await Expect(Page.Locator("#kc-error-message")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await AssertThemeShellAsync("error");

		var backLink = Page.Locator("#backToApplication");
		await Expect(backLink).ToBeVisibleAsync();

		var contrast = await backLink.EvaluateAsync<string[]>(
			"el => { const s = getComputedStyle(el); return [s.color, s.backgroundColor]; }");
		contrast[0].Should().NotBe(contrast[1],
			"error page: the back-to-application button's label must not be the same color as its fill");

		// A button that is visible but points nowhere is the same dead end with
		// extra steps. This is the assertion that actually pins the
		// properties.siteUrl fallback down - contrast alone would still pass if
		// the href collapsed back to base's empty ${client.baseUrl}.
		await Expect(backLink).ToHaveAttributeAsync("href", SiteUrl);
	}

	[Test]
	public async Task LogoutConfirm_OffersACancel()
	{
		// A confirmation with only the irreversible action on it is not a
		// confirmation. Base pairs "Sign out" with a cancel link gated on
		// ${client.baseUrl}, which is empty here, so the page shipped with one
		// button.
		var keycloak = Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/');
		await Page.GotoAsync(
			$"{keycloak}/realms/{Realm}/protocol/openid-connect/logout?client_id={FrontendClientId}");

		await Expect(Page.Locator("#kc-logout")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await AssertThemeShellAsync("logout-confirm");
		await Expect(Page.Locator("#kc-logout-cancel")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Pages_HaveDistinctBrowserTitles()
	{
		// Every template inherited login.ftl's default pageTitle, so the tab
		// read "Sign In" on the error page, the logout confirmation and the
		// verify-email page alike - and two auth pages open side by side were
		// indistinguishable in the tab strip.
		var keycloak = Fixture.GetEndpoint("keycloak").ToString().TrimEnd('/');

		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		var loginTitle = await Page.TitleAsync();

		await Page.GotoAsync(AuthUrl("registrations", locale: "en"));
		await Expect(Page.Locator("#email")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var registerTitle = await Page.TitleAsync();

		await Page.GotoAsync(
			$"{keycloak}/realms/{Realm}/protocol/openid-connect/logout?client_id={FrontendClientId}&ui_locales=en");
		await Expect(Page.Locator("#kc-logout")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var logoutTitle = await Page.TitleAsync();

		loginTitle.Should().Contain("Einsatzbereit");
		new[] { loginTitle, registerTitle, logoutTitle }.Distinct().Should().HaveCount(3,
			"each page should carry its own pageTitle rather than inheriting login.ftl's");
	}

	[Test]
	public async Task GermanPages_KeepTheProductsDuRegister()
	{
		// Keycloak's stock German addresses the user as "Sie"; the product says
		// "du" throughout. Any base message that reaches a user therefore has to
		// be overridden in messages_de.properties, and German is the default
		// served locale - so a missed override shows the mixed register to most
		// visitors, on the first screen they see. Nothing else in CI checks
		// this: the bundles are not TypeScript, so i18n-check never sees them,
		// and key parity (which is checked) says nothing about the wording.
		foreach (var (endpoint, marker) in new[] { ("auth", "#username"), ("registrations", "#email") })
		{
			await Page.GotoAsync(AuthUrl(endpoint, locale: "de"));
			await Expect(Page.Locator(marker)).ToBeVisibleAsync(new() { Timeout = 30_000 });

			var card = await Page.Locator(".auth-card").InnerTextAsync();

			// Word-boundary matched: "Sie" as its own word, not the "sie" inside
			// "diese" or a sentence-initial lowercase form.
			card.Should().NotMatchRegex(@"\bSie\b",
				$"{endpoint}: the German copy should address the user as du, not Sie");
			card.Should().NotMatchRegex(@"\bIhre[nmrs]?\b",
				$"{endpoint}: the German copy should use dein/deine, not Ihre");
		}
	}

	[Test]
	public async Task Login_HasNoSeriousA11yViolations()
	{
		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		AssertNoSeriousViolations(await Page.RunAxe(), "login");
	}

	[Test]
	public async Task Register_HasNoSeriousA11yViolations()
	{
		await Page.GotoAsync(AuthUrl("registrations", locale: "en"));
		await Expect(Page.Locator("#email")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		AssertNoSeriousViolations(await Page.RunAxe(), "register");
	}

	[Test]
	public async Task UpdatePassword_HasNoSeriousA11yViolations()
	{
		var username = $"theme-a11y-{Guid.NewGuid():N}"[..24];
		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: ["UPDATE_PASSWORD"]);
		try
		{
			await SignInThroughKeycloakUiAsync(username);
			await Expect(Page.Locator("#password-new")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			AssertNoSeriousViolations(await Page.RunAxe(), "update-password");
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}

	/// <summary>
	/// Drives the theme's own single-step login form (username and password on
	/// one page) as <paramref name="username"/>, and returns once Keycloak has
	/// moved off it - to whichever required-action page the account is carrying.
	/// </summary>
	private async Task SignInThroughKeycloakUiAsync(string username)
	{
		await Page.GotoAsync(AuthUrl(locale: "en"));
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });

		await Page.Locator("#username").FillAsync(username);
		await Page.Locator("#password").FillAsync(ThrowawayPassword);
		await Page.Locator("#kc-login").ClickAsync();

		// Waits on the login form going away rather than on a URL: the redirect
		// races the frame's own navigation, which is what made URL waits
		// intermittently flaky elsewhere in this suite (see AuthHelper.LoginAsync).
		await Expect(Page.Locator("#kc-form-login")).ToHaveCountAsync(0, new() { Timeout = 30_000 });
	}

	// Same serious/critical filter AccessibilityTests applies. Kept local
	// rather than shared: that class's list also escalates a set of moderate
	// landmark/heading rules chosen for the SPA's layout, and these pages are a
	// different document structure entirely (no header/footer landmarks, one
	// card) - inheriting that list would couple two unrelated gates.
	private static void AssertNoSeriousViolations(AxeResult result, string label)
	{
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical")
			.ToList();

		if (violations.Count == 0)
			return;

		var summary = string.Join("\n", violations.Select(v =>
			$"[{v.Impact}] {v.Id}: {v.Description}\n"
			+ string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));

		throw new Exception($"Axe found {violations.Count} a11y violation(s) on {label}:\n{summary}");
	}
}
