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
		"mt-1 block w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30";

	[Test]
	public async Task ProfilePageInput_UsesSharedInputClass()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

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
