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
			$"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		// The Page.Response listener above is the authoritative check for this
		// regression (a real 401/403 from the backend) - a page-wide GetByText
		// scan for "401" is a fragile proxy on top of it: this suite's shared,
		// PerTestSession fixture accumulates test data across the whole run, and
		// other tests (e.g. EngagementCheckInStatusCodeTests) name orgs/
		// opportunities with a random GUID suffix that can coincidentally
		// contain "401", tripping this exact assertion with no real auth error
		// present.
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
		// endpoint (created here by olaf, who has nothing to do with the admin
		// account, to prove the data isn't scoped to the caller).
		//
		// This used to also assert the created organization was visible in the
		// browser by paging through "Load more" until it turned up - flaky by
		// design: organizations are listed alphabetically and dozens of other
		// VisualTests classes constantly create their own in this same shared
		// session, so the target's page position is a moving target with no
		// fixed bound (a fresh CheckIn/Engagement/Milestone-prefixed org can
		// always sort earlier and push it further out). Asserting against the
		// raw admin-wide endpoint directly - which is what actually regressed -
		// is what this needs, not a UI scavenger hunt through a live, growing,
		// alphabetically-shifting dataset.
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
			$"{frontend.GetLeftPart(UriPartial.Authority)}/administration/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Frontend-wiring sanity check: the page actually rendered organization
		// rows rather than the error or empty state (which is what the historical
		// bug looked like - the request "succeeded" with an always-empty result).
		await Expect(Page.GetByText("Failed to load organizations.")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("No organizations found.")).Not.ToBeVisibleAsync();
		await Expect(Page.Locator("ul").First).ToBeVisibleAsync();

		// The actual regression check: call the admin-wide endpoint directly as
		// admin and confirm it returns the organization olaf just created (which
		// admin does not organize) - deterministic regardless of how many other
		// organizations exist or what order this test runs in.
		using var adminHttp = new HttpClient { BaseAddress = backendOrigin };
		adminHttp.DefaultRequestHeaders.Add(
			"Authorization", $"Bearer {await GetTokenAsync(keycloak, "admin", "admin123")}");

		var found = false;
		for (var page = 1; !found; page++)
		{
			var listResponse = await adminHttp.GetAsync($"/v1/admin/organizations?pageNumber={page}&pageSize=100");
			listResponse.EnsureSuccessStatusCode();
			var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

			var names = listBody.GetProperty("items")
				.EnumerateArray()
				.Select(o => o.GetProperty("name").GetString());
			if (names.Contains(orgName))
				found = true;
			else if (page >= listBody.GetProperty("pageCount").GetInt32())
				break;
		}

		if (!found)
			throw new Exception(
				$"'{orgName}' (created via olaf's token) was not returned by GET /v1/admin/organizations "
				+ "for the admin account across all pages - the endpoint is still scoped to the caller.");

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
