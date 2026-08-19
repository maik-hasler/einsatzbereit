using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2052: /administration/organizations, /users, /reports and
/// /audit-log all rendered the same document title and h1 ("Administration"),
/// with the section name only appearing as an h2 further down. Each route now
/// gets its own distinct title/h1 pair, matching the pattern already used
/// elsewhere (e.g. AuthGuardTests/NavigationTests' org-scoped pages).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AdministrationPageTitleTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	[Arguments("organizations", "Organizations")]
	[Arguments("users", "Users")]
	[Arguments("reports", "Reports")]
	[Arguments("audit-log", "Audit log")]
	public async Task AdministrationPage_EachSection_HasItsOwnDistinctTitleAndHeading(
		string section,
		string sectionName)
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GotoAsync($"{origin}/administration/{section}");

		await Expect(Page.Locator("h1")).ToHaveTextAsync(sectionName, new() { Timeout = 15_000 });
		await Expect(Page).ToHaveTitleAsync($"{sectionName} - Administration | Einsatzbereit");
	}
}
