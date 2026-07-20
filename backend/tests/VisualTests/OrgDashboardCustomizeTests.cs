using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the #771 review-feedback follow-up: quick actions in the
/// org-app action bar (Header.tsx's `breadcrumb.actions`, see
/// QuickActionsContext.tsx) and the customizable dashboard widget grid they
/// drive on OrgDashboardPage (add/remove/resize/reorder behind an "Edit"
/// quick action, persisted via GET/PUT .../dashboard/layout).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardCustomizeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EditQuickAction_SwapsToSaveAndCancel_AndBackOnCancel()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEdit");

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync();
		(await Page.GetByTestId("quick-action-save").CountAsync()).Should().Be(0);
		(await Page.GetByTestId("quick-action-cancel").CountAsync()).Should().Be(0);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("quick-action-cancel")).ToBeVisibleAsync();
		(await Page.GetByTestId("quick-action-edit").CountAsync()).Should().Be(0);

		// Edit mode disables the widgets' own content (see EditableWidgetTile's
		// `inert` wrapper) - the size-cycle/remove toolbar is still usable.
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")
				.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" }))
			.ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-cancel").ClickAsync();

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync();
		(await Page.GetByTestId("quick-action-save").CountAsync()).Should().Be(0);
		(await Page.GetByTestId("quick-action-cancel").CountAsync()).Should().Be(0);
	}

	[Test]
	public async Task RemovingAWidget_AndSaving_PersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRemove");

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();
		(await Page.GetByTestId("widget-tile-CreateOpportunity").CountAsync()).Should().Be(0);

		await Page.GetByTestId("quick-action-save").ClickAsync();

		// Save exits edit mode back to "Edit" once the PUT resolves.
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		(await Page.GetByTestId("widget-tile-CreateOpportunity").CountAsync()).Should().Be(0);

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		(await Page.GetByTestId("widget-tile-CreateOpportunity").CountAsync())
			.Should().Be(0, "the removed widget should stay removed after a reload - "
				+ "the layout was persisted via PUT .../dashboard/layout, not just held in local state");

		// The removed widget is offered back in the "Add a widget" panel.
		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create Opportunity" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task CancellingEdit_DiscardsRemovedWidget()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashCancel");

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();
		(await Page.GetByTestId("widget-tile-CreateOpportunity").CountAsync()).Should().Be(0);

		await Page.GetByTestId("quick-action-cancel").ClickAsync();

		// Cancelling restores the widget immediately (no API round trip needed
		// since nothing was saved) and it survives a reload untouched.
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();
	}

	private async Task CreateOrganizationAsync(string namePrefix)
	{
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one) - and guarantees a clean, empty org
		// (default dashboard layout) for deterministic widget assertions.
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });
	}
}
