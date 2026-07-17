using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SharedFormClassesTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Regression: ProfileOverviewPage and OrgSettingsPage used to each define
	// their own local "inputClass" string constant. PR #570 (#536) extracted a
	// single shared constant into lib/formClasses.ts. Assert both pages' inputs
	// still carry that exact class attribute, so a future edit to one page's
	// input styling can't silently drift from the other without touching the
	// shared module.
	private const string ExpectedInputClass =
		"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none";

	[Test]
	public async Task ProfilePageInput_UsesSharedInputClass()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Profile fields render read-only until "Edit" is clicked.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })
			.First.ClickAsync();

		var profileInput = Page.Locator("#first-name");
		await Expect(profileInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var profileInputClass = await profileInput.GetAttributeAsync("class");

		profileInputClass.Should().Be(ExpectedInputClass);
	}

	[Test]
	public async Task OrganizationSettingsInput_UsesSharedInputClass()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var orgLink = Page.GetByTestId("your-organizations-link");
		if (await orgLink.CountAsync() == 0)
			return; // olaf has no orgs in this environment, skip

		await orgLink.First.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Org general-info fields also render read-only until "Edit" is clicked.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })
			.First.ClickAsync();

		var orgInput = Page.Locator("#org-name");
		await Expect(orgInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var orgInputClass = await orgInput.GetAttributeAsync("class");

		orgInputClass.Should().Be(ExpectedInputClass);
	}
}
