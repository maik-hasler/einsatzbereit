using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
public class OrgAppRestructureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task GlobalHeader_NeverShowsOrgSwitcher_OutsideAppShell()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.Not.ToBeVisibleAsync();

		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task HomeCta_ZeroOrgs_CreatingOrgEntersItsDashboardDirectly()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var orgName = $"Visual OrgAppEntry Empty {Guid.NewGuid():N}";

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create an organization" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await createBtn.First.ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName);
	}
}
