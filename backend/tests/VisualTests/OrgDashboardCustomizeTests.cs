using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the customizable dashboard widget grid (add/remove/place
/// behind an "Edit" quick action, persisted via GET/PUT .../dashboard/layout).
/// #782 replaced the automatic skyline packer with organizer-drawn
/// corner-to-corner placement: each widget carries an explicit X/Y/Width/
/// Height, set by clicking (or tapping) two grid cells, or via the keyboard
/// (a per-widget "Move or resize" button, arrow keys to move a cursor,
/// Enter/Space to lock each corner, Escape to cancel).
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
		// `inert` wrapper) - the move/remove toolbar is still usable.
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
	public async Task GridBackdrop_OnlyRendersWhileEditing_AndHasNoLegacySizeControls()
	{
		// #782 removed the automatic packing algorithm entirely, along with
		// the manual size slider it had itself replaced (#771) - covers that
		// no manual size control exists, and that the green cell backdrop
		// (the corner-to-corner placement surface, see widgetCatalog.ts's
		// GRID_COLUMNS) only renders while editing.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashBackdrop");

		(await Page.Locator("input[type='range']").CountAsync()).Should().Be(0);
		(await Page.GetByTestId("dashboard-grid-guide-cell").CountAsync())
			.Should().Be(0, "the backdrop should not render outside edit mode");

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		(await Page.Locator("input[type='range']").CountAsync())
			.Should().Be(0, "no manual size slider or other range input should exist");
		(await Page.GetByTestId("dashboard-grid-guide-cell").CountAsync())
			.Should().BeGreaterThan(0, "the green cell backdrop should render while editing");

		await Page.GetByTestId("quick-action-cancel").ClickAsync();

		(await Page.GetByTestId("dashboard-grid-guide-cell").CountAsync())
			.Should().Be(0, "the backdrop should disappear again once editing ends");
	}

	[Test]
	public async Task DefaultLayout_WidgetTiles_RenderInsideBackdropBounds_NotStackedBelowIt()
	{
		// Regression guard for the same CSS technique the old auto-fit packer
		// relied on: a widget tile needs an explicit gridColumn/gridRow start
		// line (not just a `span N`), because the green backdrop cells claim
		// every single cell of the grid explicitly and would otherwise
		// saturate CSS Grid's auto-placement algorithm, pushing the tile into
		// a separate stack of cards below the whole backdrop. #782 still
		// relies on this (see index.tsx's explicit `col / span N` styling),
		// now sourced from the organizer's own stored placement instead of a
		// packer's output.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlay");

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var backdropCells = Page.GetByTestId("dashboard-grid-guide-cell");
		await backdropCells.First.WaitForAsync();
		var firstCellBox = await backdropCells.First.BoundingBoxAsync();
		firstCellBox.Should().NotBeNull();

		// CreateOpportunity is placed at (x=1, y=1) in DEFAULT_LAYOUT - its
		// tile's top edge should sit right at (accounting for the backdrop's
		// `-m-1` bleed) the very first backdrop cell's top edge.
		var widgetBox = await Page.GetByTestId("widget-tile-CreateOpportunity").BoundingBoxAsync();
		widgetBox.Should().NotBeNull();

		Math.Abs(widgetBox!.Y - firstCellBox!.Y).Should().BeLessThan(20,
			"the first widget should render at the top of the grid, aligned with the first backdrop "
				+ "cell - not pushed below the entire backdrop into a separate stack of cards");
	}

	[Test]
	public async Task MouseCornerPlacement_MovingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashMousePlace");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1)
		// - see placeNewWidget in widgetCatalog.ts - making the grid-cell
		// index math below trivial (an 8-column-wide, 2-row-tall grid).
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-Settings");
		await Expect(tile).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();

		// Settings starts at (x=1, y=1, width=8, height=2) - click column 2,
		// row 1 as the first corner, then column 5, row 2 as the second, to
		// move+shrink it to (x=2, y=1, width=4, height=2).
		await ClickGridCellAsync(col: 2, row: 1);
		await ClickGridCellAsync(col: 5, row: 2);

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await Expect(tile).ToHaveAttributeAsync("style", new Regex("grid-column:\\s*2\\s*/\\s*span\\s*4"));
		await Expect(tile).ToHaveAttributeAsync("style", new Regex("grid-row:\\s*1\\s*/\\s*span\\s*2"));

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-Settings"))
			.ToHaveAttributeAsync("style", new Regex("grid-column:\\s*2\\s*/\\s*span\\s*4"), new() { Timeout = 10_000 });
	}

	[Test]
	public async Task KeyboardCornerPlacement_ResizingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashKeyboardPlace");

		// ToDo starts at (x=5, y=1, width=4, height=2) in DEFAULT_LAYOUT.
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" });
		await moveButton.FocusAsync();

		// Enter/Space on the focused button advances the same state machine
		// a mouse click on a grid cell does: first press starts placing
		// (cursor defaults to the widget's current top-left corner, x=5/y=1),
		// second press locks that as the first corner, then ArrowRight moves
		// the cursor one column over before the third press commits the
		// second corner - shrinking the tile from width 4 to width 2.
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Page.Keyboard.PressAsync("ArrowRight");
		await Page.Keyboard.PressAsync("Enter");

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await Expect(tile).ToHaveAttributeAsync("style", new Regex("grid-column:\\s*5\\s*/\\s*span\\s*2"));

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-ToDo"))
			.ToHaveAttributeAsync("style", new Regex("grid-column:\\s*5\\s*/\\s*span\\s*2"), new() { Timeout = 10_000 });
	}

	[Test]
	public async Task KeyboardCornerPlacement_EscapeCancelsWithoutChangingPosition()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEscapeCancel");

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		var styleBefore = await tile.GetAttributeAsync("style");

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" });
		await moveButton.FocusAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Escape");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		(await tile.GetAttributeAsync("style")).Should().Be(styleBefore,
			"Escape must cancel the in-progress placement without changing the widget's stored position");
	}

	[Test]
	public async Task OverlappingPlacement_IsRejected_WithErrorToast_AndKeepsPreviousPosition()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlapReject");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Settings lands at (x=1, y=1, width=8, height=2); ToDo is added next
		// and lands right below it at (x=1, y=3, width=4, height=2) - see
		// placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var settingsTile = Page.GetByTestId("widget-tile-Settings");
		var styleBefore = await settingsTile.GetAttributeAsync("style");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();

		// Grow Settings down into ToDo's row (x=1..4, y=1..4) - overlaps
		// ToDo's (x=1..4, y=3..4).
		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 4, row: 4);

		await Expect(Page.GetByRole(AriaRole.Alert))
			.ToContainTextAsync("overlaps another widget");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		(await settingsTile.GetAttributeAsync("style")).Should().Be(styleBefore,
			"a rejected placement must leave the widget at its previous position");
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
		await RemoveAllWidgetsAsync();

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

	/// <summary>
	/// Clicks the grid guide cell at 1-based (col, row) - the same cells an
	/// organizer clicks to mark a placement's corners. Cells all share the
	/// "dashboard-grid-guide-cell" testid (there's no separate id per cell),
	/// so this replicates index.tsx's row-major generation order
	/// (col = i % 8 + 1, row = i / 8 + 1) to find the right one.
	/// </summary>
	private async Task ClickGridCellAsync(int col, int row)
	{
		const int gridColumns = 8;
		var index = (row - 1) * gridColumns + (col - 1);
		await Page.GetByTestId("dashboard-grid-guide-cell").Nth(index).ClickAsync();
	}

	private async Task RemoveAllWidgetsAsync()
	{
		foreach (var (testId, widgetTitle) in new[]
		{
			("CreateOpportunity", "Create Opportunity"),
			("ToDo", "Needs Your Attention"),
			("UpcomingOpportunities", "Upcoming Opportunities"),
			("Calendar", "Calendar"),
			("Settings", "Organization"),
		})
		{
			var tile = Page.GetByTestId($"widget-tile-{testId}");
			if (await tile.CountAsync() == 0) continue;
			await tile
				.GetByRole(AriaRole.Button, new() { Name = $"Remove {widgetTitle} widget" })
				.ClickAsync();
		}
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
