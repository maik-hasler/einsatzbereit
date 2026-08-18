using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #2069: OrgMembersPage's "Invite member" search
/// gated the empty-state message behind `memberSearch.length >= 4`, so a
/// query below that threshold (e.g. "ver") rendered nothing at all - no
/// results, no message, no loading indicator - while a clearly invalid
/// longer query correctly showed "No users found." The page now shows a
/// dedicated "Enter at least 4 characters" hint whenever the query is
/// non-empty but still under the threshold.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MemberSearchMinCharsHintTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MemberSearch_QueryUnderFourChars_ShowsMinCharsHint_NotNoResults()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual2069 MinCharsHint {Guid.NewGuid():N}");

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/members");
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.Locator("#member-search").FillAsync("ver");

		await Expect(Page.GetByText("Enter at least 4 characters"))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByText("No users found.", new() { Exact = true })).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("Searching…")).Not.ToBeVisibleAsync();

		var axe = await Page.RunAxe();
		axe.Violations.Where(v => v.Impact is "serious" or "critical").Should().BeEmpty();

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task MemberSearch_QueryReachesFourChars_HidesMinCharsHint()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual2069 MinCharsHintCleared {Guid.NewGuid():N}");

		await Page.RouteAsync($"**/v1/organizations/{organizationId}/members/search**", async route =>
		{
			if (route.Request.Method != "GET")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 200,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "[]",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/members");
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.Locator("#member-search").FillAsync("vera");

		await Expect(Page.GetByText("No users found.", new() { Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByText("Enter at least 4 characters")).Not.ToBeVisibleAsync();

		await Page.UnrouteAsync($"**/v1/organizations/{organizationId}/members/search**");
		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Creates an organization through the API with the signed-in user's own
	/// token - same approach as MemberSearchErrorTests.
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
