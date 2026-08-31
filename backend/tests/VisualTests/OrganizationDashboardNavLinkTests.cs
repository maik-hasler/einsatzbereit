using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationDashboardNavLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileMenu_UserWithOrg_ListsEveryOrgSection_AndNavigatesToThem()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var organizationsResponseTask = Page.WaitForResponseAsync(
			r => r.Url.EndsWith("/v1/organizations") && r.Request.Method == "GET");

		await AuthHelper.FastSignInAsync(
			Page, Fixture, frontend, "olaf", "olaf123", pinActiveOrg: false);
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await organizationsResponseTask;
		var expectedOrgId = await Fixture.GetCurrentFirstOrganizerOrganizationIdAsync(AspireFixture.OlafId);

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var banner = Page.GetByRole(AriaRole.Banner);

		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		expectedOrgId.Should().NotBeNull("olaf organizes a seeded org, so the fallback should always resolve one for him");
		var orgId = expectedOrgId!.Value;

		var entry = banner.GetByTestId("mobile-nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(entry).ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard");

		await Expect(banner.GetByRole(AriaRole.Link, new() { Name = "Opportunities", Exact = true }))
			.ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard/opportunities");

		await Expect(banner.GetByRole(AriaRole.Link, new() { Name = "Sign-ups", Exact = true }))
			.ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard/engagements");

		await Expect(banner.Locator($"a[href='/app/{orgId}/dashboard/settings']"))
			.ToHaveCountAsync(1);

		await banner.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true })
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });

		var resolvedOrgId = Guid.Parse(Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value);
		resolvedOrgId.Should().Be(orgId);
	}

	[Test]
	public async Task MobileMenu_UserWithoutOrgs_HasNoOrganizationEntry()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Administration" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByTestId("mobile-nav-organization")).ToHaveCountAsync(0);
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("mobile-nav-forOrganizations")).ToHaveCountAsync(0);
	}
}
