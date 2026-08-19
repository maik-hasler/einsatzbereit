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
	// einsatzbereit#1281: the low-contrast focus:outline-none/focus:ring-*
	// pair was dropped - the global :focus-visible ring in global.css
	// (issue #992) already overrides it, so it was dead weight, same as
	// Button.tsx's BASE_CLASSES already had it removed.
	// einsatzbereit#1104: inputClass now composes as `mt-1 block ${inputSurfaceClass}
	// text-gray-900` - inputSurfaceClass carries the shared surface recipe
	// (border/radius/background/shadow/focus) without a text color so
	// Dropdown's trigger button can reuse it, and inputClass appends
	// text-gray-900 last rather than inline among the surface classes.
	// einsatzbereit#1673: inputSurfaceClass gained a min-h-10 floor so a
	// filter row's input/select/button trio shares one height baseline.
	private const string ExpectedInputClass =
		"mt-1 block min-h-10 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 text-gray-900";

	// Regression: ProfileOverviewPage and OrgSettingsPage also used to each
	// define their own local "Field" helper component, each with its own
	// hardcoded label style ("text-sm font-medium text-gray-700") that
	// diverged from lib/formClasses.ts's "labelClass" ("text-xs font-medium
	// text-gray-600") used by every other field on the very same forms -
	// e.g. OrgSettingsPage's address block switched size/colour partway
	// down the form. einsatzbereit#1109 consolidated both local "Field"
	// components into a single shared components/Field.tsx built on
	// "labelClass". Assert both pages' field labels still carry that exact
	// class attribute, so a future edit can't silently reintroduce a
	// second label style without touching the shared module.
	private const string ExpectedLabelClass = "block text-xs font-medium text-gray-600";

	[Test]
	public async Task ProfilePageInput_UsesSharedInputClass()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Profile fields render read-only until "Edit" is clicked - the header
		// button once the profile has data, or the empty-state CTA while it
		// does not (#2066); both carry the "profile-edit" testid.
		await Page.GetByTestId("profile-edit").ClickAsync();

		var profileInput = Page.Locator("#first-name");
		await Expect(profileInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var profileInputClass = await profileInput.GetAttributeAsync("class");

		profileInputClass.Should().Be(ExpectedInputClass);

		var profileLabel = Page.Locator("label[for='first-name']");
		var profileLabelClass = await profileLabel.GetAttributeAsync("class");

		profileLabelClass.Should().Be(ExpectedLabelClass);
	}

	[Test]
	public async Task OrganizationSettingsInput_UsesSharedInputClass()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		// #771: the tab bar is gone - reach Settings via the dashboard's own link.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Org general-info fields also render read-only until "Edit" is clicked.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })
			.First.ClickAsync();

		var orgInput = Page.Locator("#org-name");
		await Expect(orgInput).ToBeVisibleAsync(new() { Timeout = 20_000 });
		var orgInputClass = await orgInput.GetAttributeAsync("class");

		orgInputClass.Should().Be(ExpectedInputClass);

		var orgLabel = Page.Locator("label[for='org-name']");
		var orgLabelClass = await orgLabel.GetAttributeAsync("class");

		orgLabelClass.Should().Be(ExpectedLabelClass);

		// einsatzbereit#1109: the address block used to be the only part of
		// this form already on "labelClass" - assert the upper field (above)
		// and this lower one now match, so the form doesn't switch label
		// styles partway down.
		var orgStreetLabel = Page.Locator("label[for='org-street']");
		var orgStreetLabelClass = await orgStreetLabel.GetAttributeAsync("class");

		orgStreetLabelClass.Should().Be(ExpectedLabelClass);
	}
}
