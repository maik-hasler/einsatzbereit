using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderBreadcrumbSharedImplementationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountPages_ReplaceActionBar_WithHeaderNavHomeAndSectionLevelEditing()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My profile", Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("header + div nav[aria-label='Breadcrumb']"))
			.ToHaveCountAsync(0);

		await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Home" }))
			.ToHaveCountAsync(0);
		var homeLink = Page.GetByTestId("nav-home");
		await Expect(homeLink).ToBeVisibleAsync();
		await Expect(homeLink).ToHaveAttributeAsync("href", "/");

		var editButton = Page.GetByTestId("profile-edit");
		await Expect(editButton).ToBeVisibleAsync();
		await Expect(Page.Locator("main section").Filter(new() { Has = editButton }))
			.ToBeVisibleAsync();

		var subNav = Page.Locator("main nav[aria-label]").First;
		foreach (var tab in new[] { "Profile", "Sign-ups" })
			await Expect(subNav.GetByRole(AriaRole.Link, new() { Name = tab })).ToBeVisibleAsync();
	}

	[Test]
	public async Task OrgAppShell_UsesTheSameBandAsEveryOtherPage_NoActionBarLeft()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardViaCtaAsync(Page, frontend);

		await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task PageHeaderBand_MakesHeaderTransparent_UntilScrolledPastTheBand()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GotoAsync($"{origin}/terms-of-use");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var header = Page.Locator("header");
		await Expect(header).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(header).ToContainClassAsync("bg-transparent");

		await Page.EvaluateAsync("() => window.scrollTo(0, 600)");

		await Expect(header).ToContainClassAsync("bg-white/95", new() { Timeout = 5_000 });

		await Page.GotoAsync($"{origin}/imprint");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(header).ToContainClassAsync("bg-transparent");

		await Page.GotoAsync($"{origin}/");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(header).ToContainClassAsync("bg-white");
	}
}
