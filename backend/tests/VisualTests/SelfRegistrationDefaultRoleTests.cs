using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1723: a brand-new account created through Keycloak's public
/// self-registration form never received the realm role "user", so every
/// authenticated endpoint (which requires it via
/// AuthorizationPolicies.EinsatzbereitDefaultUserPolicy) 403'd for organic
/// signups. Every other test in this suite signs in as vera/olaf/admin, whose
/// roles are hardcoded in the realm JSON - none of them exercised the actual
/// registration -> role-assignment path, which is why this hid. The fix adds
/// a "defaultRole" (default-roles-einsatzbereit, composite over "user") to
/// keycloak/realms/einsatzbereit-realm.json, which Keycloak grants to every
/// newly created user automatically the moment the account is created -
/// including ones created by the registration flow, unlike realm-JSON-imported
/// users.
///
/// This realm has "verifyEmail": true, which changes what the real
/// registration form asks for: Keycloak's own RegistrationPassword
/// authenticator (services/.../forms/RegistrationPassword.java,
/// isVerifyEmail() check) skips collecting a password on this form entirely
/// when email verification is enabled, and instead creates the account with
/// VERIFY_EMAIL + UPDATE_PASSWORD required actions pending - the user only
/// sets a password after clicking the emailed verification link. So the form
/// here only ever has email/username/terms fields (confirmed by driving it:
/// a "#password" field genuinely never renders). This test drives that real
/// form to completion - everything it actually asks for - then uses the
/// admin API as a test-only escape hatch (the same pattern as
/// AspireFixture.AddPlainMemberDirectlyAsync) to set a password and clear
/// the pending required actions, so the already-created account can mint a
/// token and prove the actual regression. The realm role grant this test
/// guards happens at account-creation time, when the browser form above is
/// submitted - not anywhere in the interactive verify-email/set-password UI,
/// which is orthogonal to #1723 and not what's under test here.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SelfRegistrationDefaultRoleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string Realm = "einsatzbereit";

	[Test]
	public async Task Register_CompletesForm_GrantsUserRoleForAuthenticatedApiCalls()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");
		var username = $"selfreg-{suffix}";
		var email = $"{username}@example.test";
		const string password = "Selfreg1!";

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);
		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).First.ClickAsync();

		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Page.Locator("#email").FillAsync(email);
		await Page.Locator("#username").FillAsync(username);
		await Page.Locator("#termsAccepted").CheckAsync();
		await Page.Locator("#kc-register-form input[type=submit]").ClickAsync();

		// No redirect back to the authenticated app to wait on here (see class
		// doc) - the account isn't logged in yet. The registration form having
		// navigated away is the completion signal instead.
		await Expect(Page.Locator("#kc-register-form")).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetAdminTokenAsync(keycloak)}");

		var userId = await FindUserIdAsync(adminHttp, username);
		try
		{
			await SetPasswordAndClearRequiredActionsAsync(adminHttp, userId, password);

			// The actual regression check: mint a fresh token for the account this
			// registration just created and call the baseline authenticated
			// endpoint directly. Before the fix, "user" was missing from the
			// token's roles claim and this returned 403.
			var accessToken = await AuthHelper.GetTokenAsync(keycloak, username, password);

			using var http = new HttpClient { BaseAddress = backend };
			http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
			var response = await http.GetAsync("/v1/users/me");

			if (!response.IsSuccessStatusCode)
				throw new Exception(
					$"Self-registered user '{username}' got {(int)response.StatusCode} from "
					+ "GET /v1/users/me - the realm's default role is missing 'user' (#1723).");
		}
		finally
		{
			await adminHttp.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
		}
	}

	private static async Task<string> FindUserIdAsync(HttpClient adminHttp, string username)
	{
		var response = await adminHttp.GetAsync($"/admin/realms/{Realm}/users?username={username}&exact=true");
		response.EnsureSuccessStatusCode();
		var users = await response.Content.ReadFromJsonAsync<JsonElement>();
		var first = users.EnumerateArray().FirstOrDefault();
		if (first.ValueKind != JsonValueKind.Object)
			throw new Exception($"Registration did not create a Keycloak user for username '{username}'.");

		return first.GetProperty("id").GetString()!;
	}

	private static async Task SetPasswordAndClearRequiredActionsAsync(
		HttpClient adminHttp, string userId, string password)
	{
		var updateResponse = await adminHttp.PutAsJsonAsync($"/admin/realms/{Realm}/users/{userId}", new
		{
			emailVerified = true,
			requiredActions = Array.Empty<string>(),
		});
		updateResponse.EnsureSuccessStatusCode();

		var resetPasswordResponse = await adminHttp.PutAsJsonAsync(
			$"/admin/realms/{Realm}/users/{userId}/reset-password",
			new { type = "password", value = password, temporary = false });
		resetPasswordResponse.EnsureSuccessStatusCode();
	}

}
