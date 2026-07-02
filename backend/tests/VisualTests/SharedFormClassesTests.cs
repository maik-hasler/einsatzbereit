using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SharedFormClassesTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task ProfileAndOrgSettingsInputs_ShareIdenticalClassAttribute()
	{
		// Regression: ProfileOverviewPage and OrganizationOverviewPage (settings tab)
		// used to each define their own local "inputClass" string constant. PR #570
		// (#536) extracted a single shared constant into lib/formClasses.ts. Assert the
		// two pages' inputs still carry the exact same class attribute, so a future
		// edit to one page's input styling can't silently drift from the other without
		// touching the shared module.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var profileInput = Page.Locator("#first-name");
		await Expect(profileInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var profileInputClass = await profileInput.GetAttributeAsync("class");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");

		var switcherBtn = Page.GetByLabel("Switch organization");
		if (await switcherBtn.CountAsync() == 0)
			return; // olaf has no orgs in this environment, skip

		await switcherBtn.ClickAsync();
		var settingsLink = Page.GetByTestId("org-settings-link");
		if (await settingsLink.CountAsync() == 0)
			return; // olaf has no org, skip

		await settingsLink.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/organizations/**/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var orgInput = Page.Locator("#org-name");
		await Expect(orgInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var orgInputClass = await orgInput.GetAttributeAsync("class");

		orgInputClass.Should().Be(profileInputClass);
	}
}
