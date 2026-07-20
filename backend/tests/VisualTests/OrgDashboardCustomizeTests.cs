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

		// The removed widget is offered back via the "Add Widget" quick action's
		// modal (#771 follow-up review feedback moved this from an always-visible
		// inline panel into a picker with a preview per widget).
		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("add-widget-option-CreateOpportunity"))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task AddWidgetModal_AddingAWidget_AppearsInGridAndPersistsAcrossReload()
	{
		// #771 follow-up review feedback: "Add a widget" moved from an
		// always-visible inline panel to a quick action that opens a modal
		// picker (with a small preview per widget) - this covers the actual
		// add flow through that modal end to end.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashAdd");

		// QuickCheckIn isn't part of the default layout (see DEFAULT_LAYOUT in
		// widgetCatalog.ts), so a fresh org never has it yet.
		(await Page.GetByTestId("widget-tile-QuickCheckIn").CountAsync()).Should().Be(0);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByTestId("add-widget-option-QuickCheckIn").ClickAsync();

		// Picking a widget doesn't close the picker - an organizer can add
		// several in one session - so close it explicitly.
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Expect(Page.GetByTestId("widget-tile-QuickCheckIn")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-QuickCheckIn")).ToBeVisibleAsync();
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

	[Test]
	public async Task RemovingAllWidgets_AndSaving_ShowsEmptyState_NotDefaultLayoutAfterReload()
	{
		// Regression guard for the #771 follow-up review feedback bug: an
		// organizer who removes every widget and saves that must see a
		// genuinely empty dashboard (with an "add a widget" empty state) on
		// the next load - not silently reset back to the default widget set,
		// which is what happened before HasCustomLayout distinguished "never
		// customized" from "customized to empty" (see DashboardLayoutResponse.cs).
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEmpty");

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		foreach (var (testId, widgetTitle) in new[]
		{
			("CreateOpportunity", "Create Opportunity"),
			("ToDo", "Needs Your Attention"),
			("UpcomingOpportunities", "Upcoming Opportunities"),
			("Calendar", "Calendar"),
			("Settings", "Organization"),
		})
		{
			await Page.GetByTestId($"widget-tile-{testId}")
				.GetByRole(AriaRole.Button, new() { Name = $"Remove {widgetTitle} widget" })
				.ClickAsync();
		}

		(await Page.GetByTestId("dashboard-widget-grid").CountAsync()).Should().Be(0);
		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync();

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		(await Page.GetByTestId("widget-tile-CreateOpportunity").CountAsync())
			.Should().Be(0, "the layout was saved as genuinely empty (HasCustomLayout=true) - "
				+ "it must not silently reset back to the default widget set after a reload");

		// The empty state's own CTA should get an organizer straight back into
		// edit mode with the picker open, not just the "Edit" quick action.
		await Page.GetByTestId("dashboard-empty-state")
			.GetByRole(AriaRole.Button, new() { Name = "Add a widget" })
			.ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
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
