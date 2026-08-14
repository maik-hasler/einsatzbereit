using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1224: OrgAppLayout used to funnel every org-load failure -
/// a 403, a 404, a dropped connection, a 500 - through a single .catch() into
/// one "You are not authorized" screen. It now branches on the actual
/// failure: 403 stays the "not authorized" screen, 404 says the organization
/// does not exist, and everything else gets a recoverable state with a retry
/// action instead of being mislabeled as a permissions problem.
///
/// Extended by #1774, which found the branching still collapsed one case: the
/// .catch() kept only the message string and threw the raw error away, so a
/// status NSwag generates no client branch for - notably the 400 an all-zero
/// organization id produces - fell through to the generic "something went
/// wrong" screen. The status is now what the branch reads.
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

	[Test]
	public async Task OrganizerVisitingUnknownOrgId_Gets404_SaysTheOrganizationDoesNotExist()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		await Page.GotoAsync($"{origin}/app/{Guid.NewGuid()}/dashboard");

		// #1774: this used to render the site-wide NotFoundPage ("Page not
		// found"), which is true of a URL that routes nowhere - but this URL
		// routes fine, it just names an organization that does not exist, and
		// that is what the copy has to say for the user to know whether the
		// link or the app is at fault.
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Organization not found" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.Not.ToBeVisibleAsync();
		// A route-level state is the page, so it owns the tab title. This branch
		// used to get that for free by rendering NotFoundPage, which sets one;
		// dropping to a chrome-less state component would have silently left the
		// tab reading the bare app name.
		await Expect(Page).ToHaveTitleAsync("Organization not found | Einsatzbereit");
	}

	/// <summary>
	/// The exact #1774 F10 repro: an all-zero GUID is a well-formed route value
	/// (so ASP.NET's <c>:guid</c> constraint matches and the request reaches the
	/// handler) but not a valid OrganizationId, so <c>OrganizationId.Create</c>
	/// rejects it with a 400 before any membership check runs. NSwag generates
	/// no 400 branch for this endpoint, so the client throws a bare
	/// ApiException - which is precisely the rejection whose status the layout
	/// used to discard, landing every visitor here on the generic crash screen
	/// complete with a retry button that could only ever fail the same way.
	/// </summary>
	[Test]
	public async Task VisitingAllZeroOrgId_Gets400_SaysTheOrganizationDoesNotExist()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		await Page.GotoAsync($"{origin}/app/{Guid.Empty}/dashboard");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Organization not found" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Something went wrong" }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.Not.ToBeVisibleAsync();
	}

	/// <summary>
	/// #1774 F33, org-shell half: with no connection the org request fails like
	/// any other transport error, but calling that "an unexpected error" and
	/// offering a retry is a lie - the retry cannot succeed until the connection
	/// is back. Simulated by pinning <c>navigator.onLine</c> false and aborting
	/// the org request rather than by <c>Context.SetOfflineAsync</c>, because
	/// this suite blocks service workers (see VisualTestBase.ContextOptions), so
	/// a genuinely offline document navigation could not load the app shell at
	/// all - the very thing the precache makes work in production.
	/// </summary>
	[Test]
	public async Task OrgShellWhileOffline_ShowsOfflineState_WithoutARetryThatCannotWork()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var organizationId = await CreateOrganizationAsync("OrgOffline");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		await Page.AddInitScriptAsync(
			"Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });");
		await Page.RouteAsync($"**/v1/organizations/{organizationId}", route =>
			route.AbortAsync("internetdisconnected"));

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("An unexpected error occurred", new() { Exact = false }))
			.Not.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" }))
			.Not.ToBeVisibleAsync();
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
