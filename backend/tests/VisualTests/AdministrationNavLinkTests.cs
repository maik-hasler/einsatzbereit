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
		await Page.WaitForURLAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/administration");
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
}
