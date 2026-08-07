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
/// a "defaultRole" (default-roles-einsatzbereit, composite over "user" plus
/// Keycloak's own offline_access/uma_authorization/account defaults) to
/// keycloak/realms/einsatzbereit-realm.json, which Keycloak grants to every
/// newly created user automatically - including ones created by the
/// registration flow, unlike realm-JSON-imported users.
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
		await Page.Locator("#password").FillAsync(password);
		await Page.Locator("#password-confirm").FillAsync(password);
		await Page.Locator("#termsAccepted").CheckAsync();
		await Page.Locator("#kc-register-form input[type=submit]").ClickAsync();

		// Same completion signal LoginAsync uses - the SPA lands back on "/"
		// already authenticated once Keycloak finishes the registration and
		// redirects with an auth code.
		await Page.WaitForURLAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/", new() { Timeout = 30_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" })).ToBeVisibleAsync();

		string? userId = null;
		try
		{
			// The actual regression check: mint a fresh token for the account this
			// registration just created and call the baseline authenticated
			// endpoint directly. Before the fix, "user" was missing from the
			// token's roles claim and this returned 403.
			var (accessToken, sub) = await GetTokenAsync(keycloak, username, password);
			userId = sub;

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
			if (userId is not null)
				await DeleteUserAsync(keycloak, userId);
		}
	}

	private static async Task<(string AccessToken, string Sub)> GetTokenAsync(
		Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
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
		var accessToken = body.GetProperty("access_token").GetString()!;
		var sub = AuthHelper.DecodeJwtPayload(accessToken).GetProperty("sub").GetString()!;
		return (accessToken, sub);
	}

	private static async Task DeleteUserAsync(Uri keycloak, string userId)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var tokenResponse = await http.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "client_credentials",
				["client_id"] = "backend",
				["client_secret"] = "backend-secret",
			}));
		tokenResponse.EnsureSuccessStatusCode();
		var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
		var adminToken = tokenBody.GetProperty("access_token").GetString();

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {adminToken}");
		await adminHttp.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
	}
}
