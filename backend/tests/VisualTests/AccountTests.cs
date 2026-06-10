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

		await Expect(Page.GetByLabel("Username")).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByLabel("Email address")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeVisibleAsync(new() { Timeout = 5_000 });
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

		await Page.GetByLabel("First name").FillAsync("Vera", new() { Timeout = 20_000 });
		await Page.GetByLabel("Last name").FillAsync("Sample");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

		await Expect(Page.GetByText("Profile saved.")).ToBeVisibleAsync();
	}
}
