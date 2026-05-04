using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccountTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task AccountPage_ShowsProfileForm_WhenAuthenticated()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "hannah", "hannah123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByLabel("Benutzername")).ToBeVisibleAsync();
		await Expect(Page.GetByLabel("E-Mail-Adresse")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task AccountPage_DisplaysUsername_AfterLogin()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "hannah", "hannah123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByText("@hannah")).ToBeVisibleAsync();
	}

	[Test]
	public async Task AccountPage_SavesProfileChanges()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "hannah", "hannah123");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByLabel("Vorname").FillAsync("Hannah");
		await Page.GetByLabel("Nachname").FillAsync("Muster");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

		await Expect(Page.GetByText("Änderungen gespeichert.")).ToBeVisibleAsync();
	}
}
