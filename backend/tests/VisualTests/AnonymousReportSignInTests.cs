using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

// #2061: the "Report" button on both pages used to be hidden entirely from
// anonymous visitors (auth.isAuthenticated &&) even though they are the ones
// most likely to encounter spam - the broken "Melden" promise /help and
// /contact both made. The button now always renders and routes an anonymous
// click through sign-in first, since reporting itself still requires an
// authenticated account on the backend (EinsatzbereitDefaultUserPolicy).
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AnonymousReportSignInTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task VolunteerOpportunityDetailPage_AnonymousReportClick_RedirectsToKeycloakSignIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("no opportunities seeded");
		}

		var href = await firstCard.GetAttributeAsync("href");
		Skip.When(href is null, "opportunity card had no href attribute");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var reportButton = Page.GetByTestId("report-opportunity");
		await Expect(reportButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Registered before the click so signinRedirect()'s own discovery
		// fetch and the /auth navigation it triggers both get through -
		// see AuthHelper.AllowKeycloakCrossOriginRequestsAsync.
		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);
		await reportButton.ClickAsync();

		// Wait on the Keycloak login form element, not the URL - race-prone
		// against the JS-driven redirect (same reasoning as
		// AuthGuardTests.MyEngagements_Anonymous_RedirectsToKeycloak). Proves
		// the click opened the sign-in flow rather than the report modal.
		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page).ToHaveURLAsync(new Regex(@"/realms/einsatzbereit/protocol/openid-connect/auth"));
	}

	[Test]
	public async Task OrganizationProfilePage_AnonymousReportClick_RedirectsToKeycloakSignIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Expect(Page.Locator("h1").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The org link is the z-20 one, not the stretched card-cover Link that
		// covers the whole card (see frontend/AGENTS.md's clickable-card
		// convention) - matching by testid rather than by role would pick the
		// cover link and navigate to the opportunity instead.
		var orgLink = Page.GetByTestId("opportunity-org-link").First;
		await Expect(orgLink).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var href = await orgLink.GetAttributeAsync("href");

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var reportButton = Page.GetByTestId("report-organization");
		await Expect(reportButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);
		await reportButton.ClickAsync();

		await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
		await Expect(Page).ToHaveURLAsync(new Regex(@"/realms/einsatzbereit/protocol/openid-connect/auth"));
	}
}
