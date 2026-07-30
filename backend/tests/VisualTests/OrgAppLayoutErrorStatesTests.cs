using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1224: OrgAppLayout used to funnel every org-load failure -
/// a 403, a 404, a dropped connection, a 500 - through a single .catch() into
/// one "You are not authorized" screen. It now branches on the actual
/// failure: 403 stays the "not authorized" screen, 404 renders the shared
/// NotFoundPage, and everything else gets a recoverable state with a retry
/// action instead of being mislabeled as a permissions problem.
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
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to Einsatzbereit" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task OrganizerVisitingUnknownOrgId_Gets404_ShowsNotFoundPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		await Page.GotoAsync($"{origin}/app/{Guid.NewGuid()}/dashboard");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task ServerError_ShowsRecoverableStateWithRetry_AndRetrySucceeds()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var organizationId = await CreateOrganizationAsync("Org500Retry");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		var shouldFail = true;
		await Page.RouteAsync($"**/v1/organizations/{organizationId}", async route =>
		{
			if (route.Request.Method != "GET" || !shouldFail)
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Something went wrong" });
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 15_000 });
		// Not the "not authorized" screen - the whole point of the fix (#1224).
		await Expect(Page.GetByText("You don't have access to this organization.")).Not.ToBeVisibleAsync();

		var retryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Try again" });
		await Expect(retryButton).ToBeVisibleAsync();

		shouldFail = false;
		await retryButton.ClickAsync();

		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(heading).Not.ToBeVisibleAsync();
	}

	private async Task<string> CreateOrganizationAsync(string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var response = await http.PostAsJsonAsync("/v1/organizations", new
		{
			name = $"VisualTests {label} {suffix}",
		});
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()
			?? throw new InvalidOperationException("Created organization had no id.");
	}
}
