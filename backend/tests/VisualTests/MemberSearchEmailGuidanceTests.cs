using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1894: OrgMembersPage's "Invite member" search
/// showed the same generic "No users found." empty state for a typo'd query
/// and for a syntactically valid but unregistered email, even though the
/// field's placeholder implies email-based invites work. An email-shaped
/// query with zero results now gets dedicated guidance pointing the
/// organizer at signing the person up first, while a non-email query with
/// zero results still shows the generic message.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class MemberSearchEmailGuidanceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MemberSearch_UnregisteredEmail_ShowsSignUpGuidance_NotGenericNoResults()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual1894 EmailGuidance {Guid.NewGuid():N}");

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

		await Page.Locator("#member-search").FillAsync("brandnewperson@example.com");

		var emailGuidance = Page.GetByText(
			"No account exists for this email yet - ask the person to sign up first, then you can invite them here.");
		await Expect(emailGuidance).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByText("No users found.", new() { Exact = true })).Not.ToBeVisibleAsync();

		var axe = await Page.RunAxe();
		axe.Violations.Where(v => v.Impact is "serious" or "critical").Should().BeEmpty();

		await Page.UnrouteAsync($"**/v1/organizations/{organizationId}/members/search**");
		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task MemberSearch_NonEmailQueryWithNoResults_ShowsGenericNoResults()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual1894 GenericNoResults {Guid.NewGuid():N}");

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

		await Page.Locator("#member-search").FillAsync("nosuchuser");

		await Expect(Page.GetByText("No users found.", new() { Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByText("ask the person to sign up", new() { Exact = false }))
			.Not.ToBeVisibleAsync();

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
