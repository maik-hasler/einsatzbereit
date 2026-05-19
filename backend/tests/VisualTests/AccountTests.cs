using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccountTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountPage_ShowsProfileForm_WhenAuthenticated()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByLabel("Username")).ToBeVisibleAsync();
		await Expect(Page.GetByLabel("Email address")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task AccountPage_DisplaysUsername_AfterLogin()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");

		await Expect(Page.GetByLabel("Username")).ToHaveValueAsync("vera",
			new() { Timeout = 30_000 });
	}

	[Test]
	public async Task AccountPage_SavesProfileChanges()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByLabel("First name").FillAsync("Vera");
		await Page.GetByLabel("Last name").FillAsync("Sample");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

		await Expect(Page.GetByText("Changes saved.")).ToBeVisibleAsync();
	}
}
