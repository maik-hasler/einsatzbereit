using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1942: OrgMembersPage's "Invite member" search
/// unconditionally cleared the candidate list in its request `.catch()`, so a
/// failed search (e.g. a 400) rendered identically to a search that genuinely
/// found nobody - both showed "No users found.", with no indication anything
/// had gone wrong. The page now tracks a dedicated `memberSearchError` state
/// and shows an ErrorBanner instead of the empty-results message when the
/// request itself fails.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MemberSearchErrorTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MemberSearch_FailedRequest_ShowsErrorBanner_NotNoResults()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual1942 MemberSearchError {Guid.NewGuid():N}");

		await Page.RouteAsync($"**/v1/organizations/{organizationId}/members/search**", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 400,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"status\":400}",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/members");
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.Locator("#member-search").FillAsync("admin");

		var banner = Page.GetByRole(AriaRole.Alert).Filter(new() { HasTextString = "Could not search for users." });
		await Expect(banner).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// The whole point of #1942: a failed request must never be mistaken for
		// a search that genuinely found nobody.
		await Expect(Page.GetByText("No users found.")).Not.ToBeVisibleAsync();

		// A failed *request* state needs the real backend to fail, so it can't
		// move down to a component scan - and this test has already built it,
		// so scan it here rather than standing up the same route intercept a
		// second time elsewhere (same rationale as OrgSettingsFormActionsTests's
		// own failed-save test).
		var axe = await Page.RunAxe();
		axe.Violations.Where(v => v.Impact is "serious" or "critical").Should().BeEmpty();

		await Page.UnrouteAsync($"**/v1/organizations/{organizationId}/members/search**");
		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Creates an organization through the API with the signed-in user's own
	/// token - same approach as OrgSettingsFormActionsTests, faster than
	/// driving the switcher's create-organization dialog.
	/// </summary>
	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	/// <summary>
	/// The shared olaf account accumulates test debris across this suite's
	/// session (see the root AGENTS.md note about live staging) - clean up the
	/// organizations this test creates.
	/// </summary>
	private async Task DeleteOrganizationAsync(Uri backend, string organizationId)
	{
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		await http.DeleteAsync($"/v1/organizations/{organizationId}");
	}

	private async Task<HttpClient> CreateAuthenticatedHttpClientAsync(Uri backend)
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		return http;
	}
}
