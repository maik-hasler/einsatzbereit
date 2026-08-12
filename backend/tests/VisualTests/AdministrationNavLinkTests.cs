using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for the PR #768 review feedback: admins had no way to reach
/// /administration except by typing the URL directly - no nav entry linked
/// to it anywhere. Covers both that admin now sees it in the account dropdown
/// and that a non-admin (e.g. vera) still does not.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AdministrationNavLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountDropdown_AuthenticatedAdmin_ShowsAdministrationLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).ClickAsync();

		var link = Page.GetByRole(AriaRole.Link, new() { Name = "Administration" });
		await Expect(link).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await link.ClickAsync();
		// /administration is a shell that redirects to its first section.
		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/administration/organizations");
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Administration");
	}

	[Test]
	public async Task AccountDropdown_AuthenticatedNonAdmin_HasNoAdministrationLink()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My Profile" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Administration" }))
			.Not.ToBeVisibleAsync();
	}

	// Regression for #1026: the nav link was already hidden for non-admins
	// (see the test above), but /administration itself had no role check -
	// a non-admin who typed the URL directly still got the page shell (the
	// "Administration" heading) with every section immediately failing its
	// API call and rendering an error banner, instead of being kept off the
	// page entirely. ProtectedRoute's requiredRole="admin" now keeps such
	// visitors out before AdministrationPage ever mounts.
	//
	// Amended by #1774: keeping them out used to mean <Navigate to="/" />,
	// which silently dumped anyone following a bookmarked or shared admin link
	// on the landing page - nothing there distinguished "you may not go
	// there" from "that link is dead" or "you got signed out". The guard now
	// holds the requested URL and says which of the three it is, so the link
	// stays in the address bar to hand to someone whose account can open it.
	[Test]
	public async Task Administration_DirectNavigationAsNonAdmin_StaysOnTheUrlAndExplainsWhy()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync($"{origin}/administration");

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Admin rights required" }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("Your account does not have admin rights", new() { Exact = false }))
			.ToBeVisibleAsync();
		// The page itself still never mounts - the point of #1026 stands.
		await Expect(Page.Locator("h1")).Not.ToHaveTextAsync("Administration");
		// ...and the URL is still the one that was asked for, rather than "/".
		await Expect(Page).ToHaveURLAsync($"{origin}/administration");
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to home" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task Administration_DirectNavigationAsAdmin_ShowsAdministrationPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync($"{origin}/administration");

		await Page.WaitForURLAsync($"{origin}/administration/organizations", new() { Timeout = 15_000 });
		await Expect(Page.Locator("h1")).ToHaveTextAsync("Administration");
	}
}
