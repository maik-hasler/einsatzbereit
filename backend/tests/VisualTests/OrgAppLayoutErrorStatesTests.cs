using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The one org-shell failure that also proves something about the *backend*:
/// a non-member really does get a 403 from GET /v1/organizations/{id}, and
/// the shell turns that into the "not authorized" screen rather than a
/// generic error.
///
/// #1224: this layout used to funnel every org-load failure - a 403, a 404, a
/// dropped connection, a 500 - through a single .catch() into one "You are not
/// authorized" screen. It branches on the actual status now. The other six
/// cases, which each mocked one status and asserted which screen came back,
/// moved to <c>frontend/src/layouts/OrgAppLayout.test.tsx</c> in
/// einsatzbereit#2148.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppLayoutErrorStatesTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NonOrganizerVisitingOrgApp_Gets403_ShowsNotAuthorizedScreen()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var organizationId = await CreateOrganizationAsync("Org403Screen");

		// vera is a plain "user" (no organisator role), so
		// GetOrganizationDetails' EinsatzbereitOrganisatorPolicy rejects her
		// with 403 regardless of which organization she targets - the
		// permanent, non-recoverable case this screen is for.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		await Expect(Page.GetByRole(AriaRole.Heading,
			new() { Name = "You don't have access to this organization." }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		// #1774: the heading alone never said what to do about it. It now
		// carries the reason - not a member - and who can change that.
		await Expect(Page.GetByText("You are not a member of this organization.", new() { Exact = false }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to Einsatzbereit" }))
			.ToBeVisibleAsync();
		// A permissions problem is permanent - retrying the same request as the
		// same user cannot change the answer, so no retry is offered.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.Not.ToBeVisibleAsync();
	}

	private async Task<string> CreateOrganizationAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new
		{
			name = $"VisualTests {label} {suffix}",
		});
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");
	}
}
