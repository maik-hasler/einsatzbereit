using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class JwtAudienceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagements_AuthenticatedVera_LoadsWithoutAuthError()
	{
		// Regression: before PR #476 the backend had ValidateAudience = false.
		// Now tokens must include aud=backend (added via Keycloak audience mapper).
		// A 401 response here means the mapper is missing or validation is broken.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend").GetLeftPart(UriPartial.Authority);

		var authErrors = new List<string>();
		Page.Response += (_, resp) =>
		{
			if (resp.Url.StartsWith(backend, StringComparison.Ordinal)
				&& (resp.Status == 401 || resp.Status == 403))
				authErrors.Add($"{resp.Status} {resp.Url}");
		};

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		await Expect(Page.GetByText("401")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("Unauthorized")).Not.ToBeVisibleAsync();

		if (authErrors.Count > 0)
			throw new Exception(
				$"JWT audience validation rejected {authErrors.Count} request(s): "
				+ string.Join(", ", authErrors));
	}

	[Test]
	public async Task Administration_AuthenticatedAdmin_LoadsOrganizationsWithoutAuthError()
	{
		// Regression for #760: the "admin" realm role had no composite roles,
		// so an admin-only token was missing "user"/"organisator" and every
		// baseline authenticated endpoint returned 403 for the admin account
		// specifically.
		//
		// Also a regression for the PR #768 review feedback: the admin
		// organizations list used to call GET /v1/organizations, which is
		// scoped to "organizations the caller organizes" - always empty for
		// admin, who organizes nothing. It now calls a real admin-wide listing
		// endpoint, so a pre-existing organization (created here by olaf, who
		// has nothing to do with the admin account) must show up in it.
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var backendOrigin = Fixture.GetEndpoint("backend");
		var backend = backendOrigin.GetLeftPart(UriPartial.Authority);
		var orgName = $"Visual768 Administration {Guid.NewGuid():N}";

		using var olafHttp = new HttpClient { BaseAddress = backendOrigin };
		olafHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();

		var authErrors = new List<string>();
		Page.Response += (_, resp) =>
		{
			if (resp.Url.StartsWith(backend, StringComparison.Ordinal)
				&& (resp.Status == 401 || resp.Status == 403))
				authErrors.Add($"{resp.Status} {resp.Url}");
		};

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");

		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/administration");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("Failed to load organizations.")).Not.ToBeVisibleAsync();

		// Organizations are listed alphabetically and other tests in this shared
		// session constantly create their own - this one could land on any page,
		// not just the first. Page through via "Load more" until it turns up.
		var orgLocator = Page.GetByText(orgName);
		var loadMoreButton = Page.GetByRole(AriaRole.Button, new() { Name = "Load more" });
		for (var i = 0; i < 20 && await orgLocator.CountAsync() == 0; i++)
		{
			if (await loadMoreButton.CountAsync() == 0)
				break;
			await loadMoreButton.ClickAsync();
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		}
		await Expect(orgLocator).ToBeVisibleAsync(new() { Timeout = 15_000 });

		if (authErrors.Count > 0)
			throw new Exception(
				$"Admin token rejected on baseline endpoint(s): "
				+ string.Join(", ", authErrors));
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
