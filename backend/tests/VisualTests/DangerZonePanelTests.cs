using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1792: the shared DangerZonePanel used to be headed
/// "Danger zone" ("Gefahrenzone" in German - a calque of GitHub's label that
/// reads like a translation artefact) and set its entire explanation in
/// text-red-700, so the error colour covered body prose instead of being
/// confined to the signal. The heading is per-surface now ("Delete account" /
/// "Delete organization") and the description is body colour, while the
/// heading and the destructive button still carry the red.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class DangerZonePanelTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Both surfaces render the same component, so both must show the same
	// classes - a future edit that reddens the copy on one page only would
	// have to go through DangerZonePanel.tsx and fail here for both.
	private const string ExpectedDescriptionClass = "mb-4 text-sm text-gray-600";
	private const string ExpectedHeadingClass = "mb-1 text-base font-semibold text-red-800";

	[Test]
	public async Task ProfileSettingsPage_DangerZone_IsHeadedDeleteAccountWithBodyColourCopy()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Delete account" });
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 20_000 });

		await AssertPanelColoursAsync(heading, "Profile settings danger zone");

		// The destructive action keeps its own wording next to the heading.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Delete my account" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task OrgSettingsPage_DangerZone_IsHeadedDeleteOrganizationWithBodyColourCopy()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Delete organization" });
		await Expect(heading).ToBeVisibleAsync(new() { Timeout = 20_000 });

		await AssertPanelColoursAsync(heading, "Organization settings danger zone");

		// #1792's decision note: the heading and the button below it must not
		// disagree on casing, so the button is sentence case too.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Delete organization" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	private async Task AssertPanelColoursAsync(ILocator heading, string label)
	{
		var headingClass = await heading.GetAttributeAsync("class");
		headingClass.Should().Be(ExpectedHeadingClass, $"{label}: the heading carries the error colour");

		var description = heading.Locator("xpath=following-sibling::p[1]");
		var descriptionClass = await description.GetAttributeAsync("class");
		descriptionClass.Should().Be(
			ExpectedDescriptionClass,
			$"{label}: the explanation is body copy, not an error message");
	}
}
