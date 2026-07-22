using System.Net.Http.Json;
using System.Text.Json;
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
		await AssertWidgetOccupiesCellsAsync("Settings", x: 2, y: 1, width: 4, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("Settings", x: 2, y: 1, width: 4, height: 2);
	}

	[Test]
	public async Task PointerDrag_MovingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		// #16: a real press-and-drag on the grip button, distinct from the
		// click-click-click flow covered above - moves the widget live under
		// the pointer and commits on release, with no corner-picking banner
		// involved at all.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashPointerDrag");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=2) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		var tileBox = await tile.BoundingBoxAsync();
		tileBox.Should().NotBeNull();
		var colPx = tileBox!.Width / 4;

		var grip = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" });
		var gripBox = await grip.BoundingBoxAsync();
		gripBox.Should().NotBeNull();
		var startX = gripBox!.X + gripBox.Width / 2;
		var startY = gripBox.Y + gripBox.Height / 2;

		// Drag two grid columns to the right (x=1 -> x=3) - well clear of the
		// DRAG_THRESHOLD_PX below which a press+release is read as a plain
		// click instead. Several intermediate moves rather than one jump, so
		// the live preview actually gets a chance to update along the way.
		await Page.Mouse.MoveAsync(startX, startY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(startX + colPx, startY, new() { Steps = 5 });
		await Page.Mouse.MoveAsync(startX + colPx * 2, startY, new() { Steps = 5 });
		await Page.Mouse.UpAsync();

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 3, y: 1, width: 4, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 3, y: 1, width: 4, height: 2);
	}

	[Test]
	public async Task EdgeResizeHandles_ResizeOnlyTheirOwnAxis_AndPersistAcrossReload()
	{
		// #830 follow-up on the widget-placement UX: alongside the existing
		// corner handle (which resizes both axes together), dedicated
		// right-edge and bottom-edge handles let an organizer change just the
		// width or just the height - a more familiar direct-manipulation
		// pattern (like a spreadsheet column border) than only ever having a
		// single corner dot for both dimensions at once.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEdgeResize");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=2) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		var tileBox = await tile.BoundingBoxAsync();
		tileBox.Should().NotBeNull();
		var colPx = tileBox!.Width / 4;
		var rowPx = tileBox.Height / 2;

		// Drag the right-edge handle one column further right: width 4 -> 5,
		// height must stay exactly 2.
		var widthHandle = tile.GetByTestId("widget-resize-handle-width");
		var widthHandleBox = await widthHandle.BoundingBoxAsync();
		widthHandleBox.Should().NotBeNull();
		var widthStartX = widthHandleBox!.X + widthHandleBox.Width / 2;
		var widthStartY = widthHandleBox.Y + widthHandleBox.Height / 2;

		await Page.Mouse.MoveAsync(widthStartX, widthStartY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(widthStartX + colPx, widthStartY, new() { Steps = 5 });
		await Page.Mouse.UpAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 5, height: 2);

		// Drag the bottom-edge handle one row further down: height 2 -> 3,
		// width must stay exactly the 5 the previous step just set.
		tileBox = await tile.BoundingBoxAsync();
		tileBox.Should().NotBeNull();
		var heightHandle = tile.GetByTestId("widget-resize-handle-height");
		var heightHandleBox = await heightHandle.BoundingBoxAsync();
		heightHandleBox.Should().NotBeNull();
		var heightStartX = heightHandleBox!.X + heightHandleBox.Width / 2;
		var heightStartY = heightHandleBox.Y + heightHandleBox.Height / 2;

		await Page.Mouse.MoveAsync(heightStartX, heightStartY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(heightStartX, heightStartY + rowPx, new() { Steps = 5 });
		await Page.Mouse.UpAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 5, height: 3);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 5, height: 3);
	}

	[Test]
	public async Task RemovingAWidget_AutomaticallyClosesTheHorizontalGapNextToIt()
	{
		// #830 follow-up on the widget-placement UX: compaction used to only
		// close gaps vertically (sliding widgets up), so removing or shrinking
		// a widget could leave a horizontal hole next to it that nothing ever
		// reflowed into - the grid only felt "automatic" on one axis. DEFAULT_
		// LAYOUT places CreateOpportunity and ToDo side by side in the same
		// row (x=1/x=5, both width=4) - removing CreateOpportunity should now
		// slide ToDo all the way left into column 1, not leave it at column 5
		// with an empty gap to its left.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashHorizontalCompact");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 4, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 4, height: 2);
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
		// second press locks that as the first corner, then ArrowRight and
		// ArrowDown move the cursor to (col=6, row=2) before the third press
		// commits the second corner there - shrinking the tile from
		// (width=4, height=2) to (width=2, height=2) while keeping the same
		// top-left corner.
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Page.Keyboard.PressAsync("ArrowRight");
		await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Enter");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 5, y: 1, width: 2, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 5, y: 1, width: 2, height: 2);
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
	public async Task OverlappingPlacement_DisplacesTheOtherWidgetDownward_InsteadOfBeingRejected()
	{
		// #18: an overlapping placement used to be rejected outright - it now
		// pushes whatever's in the way straight down instead (then closes any
		// gap that leaves further up, see #14's compaction), and persists
		// that displacement across a reload just like any other placement.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlapPush");

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

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();

		// Grow Settings down into ToDo's row (x=1..4, y=1..4) - overlaps
		// ToDo's (x=1..4, y=3..4). ToDo has nowhere else to go but straight
		// down below the grown Settings tile, landing at (x=1..4, y=5..6).
		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 4, row: 4);

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		(await Page.GetByRole(AriaRole.Alert).CountAsync()).Should().Be(0,
			"an overlapping placement is displaced, not rejected - no error toast should appear");
		await AssertWidgetOccupiesCellsAsync("Settings", x: 1, y: 1, width: 4, height: 4);
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 5, width: 4, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("Settings", x: 1, y: 1, width: 4, height: 4);
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 5, width: 4, height: 2);
	}

	[Test]
	public async Task ResizingBelowAWidgetsMinimumSize_IsRejected_WithErrorToast_AndKeepsPreviousPosition()
	{
		// #15/#18: overlap alone no longer rejects a placement, but a widget's
		// restored per-type minimum size still does - there's nowhere to
		// "push" a widget that's shrunk smaller than it can usefully render.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashMinSizeReject");

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Calendar").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var calendarTile = Page.GetByTestId("widget-tile-Calendar");
		var styleBefore = await calendarTile.GetAttributeAsync("style");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Calendar" }).ClickAsync();

		// Calendar's minimum is 4x4 (see WIDGET_CATALOG in widgetCatalog.ts) -
		// shrink it to 3x3 (x=1..3, y=1..3), below that floor on both axes.
		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 3, row: 3);

		await Expect(Page.GetByRole(AriaRole.Alert))
			.ToContainTextAsync("doesn't fit");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		(await calendarTile.GetAttributeAsync("style")).Should().Be(styleBefore,
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

	[Test]
	public async Task WidgetContentOverflow_DoesNotStretchTheSharedGridRow()
	{
		// Follow-up review feedback on #782/#787's redesign: the grid used to
		// size its rows with auto-rows-[minmax(64px,auto)], which grows the
		// WHOLE row band - every column, not just the cell whose content
		// demanded the extra height - so one widget with more content than
		// its allotted rows used to stretch the green backdrop guide cells
		// (and every sibling sharing that row) too, making edit mode look
		// randomly, inconsistently sized. The grid row height is now fixed
		// (see index.tsx), so an overflowing widget's own WidgetCard content
		// area scrolls internally instead - this publishes enough
		// opportunities to overflow UpcomingOpportunitiesWidget (height=3,
		// MAX_ITEMS=5) and confirms every backdrop row still shares the
		// same rendered height as an unaffected row.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRowHeight");
		var organizationId = new Regex(@"/app/([^/]+)/dashboard")
			.Match(Page.Url).Groups[1].Value;

		var token = await GetAccessTokenAsync();
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		for (var i = 0; i < 5; i++)
		{
			var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				title = $"Row Height Opportunity {i} {suffix}",
				description = "Created by WidgetContentOverflow_DoesNotStretchTheSharedGridRow",
				organizationId,
				isRemote = true,
				occurrence = "OneTime",
				participationType = "Waitlist",
				checkInMethod = "None",
				isDraft = true,
				tags = new[] { $"visual-row-height-{suffix}" },
			});
			oppResponse.EnsureSuccessStatusCode();
			var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
			var opportunityId = opportunity.GetProperty("id").GetString();

			var start = DateTimeOffset.UtcNow.AddDays(3 + i);
			(await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
				new { startDateTime = start, endDateTime = start.AddHours(2), maxParticipants = 5, recurrenceCount = 1 }))
				.EnsureSuccessStatusCode();

			(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
				.EnsureSuccessStatusCode();
		}

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-UpcomingOpportunities")
				.GetByText("Row Height Opportunity 4"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("dashboard-grid-guide-cell").First).ToBeVisibleAsync();

		// UpcomingOpportunities sits at y=3..5 in DEFAULT_LAYOUT - row 1 (host
		// to CreateOpportunity/ToDo, neither of which grew) is the unaffected
		// baseline; row 4 falls inside the now-overflowing widget's own rows.
		const int gridColumns = 8;
		var row1CellHeight = await Page.GetByTestId("dashboard-grid-guide-cell")
			.Nth((1 - 1) * gridColumns).EvaluateAsync<double>("el => el.getBoundingClientRect().height");
		var row4CellHeight = await Page.GetByTestId("dashboard-grid-guide-cell")
			.Nth((4 - 1) * gridColumns).EvaluateAsync<double>("el => el.getBoundingClientRect().height");

		Math.Abs(row1CellHeight - row4CellHeight).Should().BeLessThan(2,
			"every grid row must share the same fixed height, even when a widget's own content "
				+ "(here, 5 published opportunities in a height=3 widget) overflows its allotted rows - "
				+ "that overflow should scroll within the widget's own card, not stretch the shared row band");
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
		await Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(col, row)).ClickAsync();
	}

	/// <summary>
	/// Asserts a widget tile's rendered bounds line up with the backdrop
	/// cells at its expected 1-based (x, y, width, height) grid placement.
	/// Deliberately does NOT inspect the tile's "style" attribute - browsers
	/// are free to serialize the separately-set gridColumn/gridRow inline
	/// styles into a combined "grid-area" shorthand (observed on CI's
	/// Chromium build), so asserting on the raw attribute string would be
	/// coupled to that serialization choice rather than the actual layout.
	/// Comparing rendered pixel bounds against the same backdrop cells the
	/// organizer clicked to make the placement is what
	/// DefaultLayout_WidgetTiles_RenderInsideBackdropBounds_NotStackedBelowIt
	/// above already does for the same reason.
	/// </summary>
	private async Task AssertWidgetOccupiesCellsAsync(string widgetTestId, int x, int y, int width, int height)
	{
		var tile = Page.GetByTestId($"widget-tile-{widgetTestId}");
		var topLeftCell = Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(x, y));
		var bottomRightCell = Page.GetByTestId("dashboard-grid-guide-cell")
			.Nth(GridCellIndex(x + width - 1, y + height - 1));

		var tileBox = await tile.BoundingBoxAsync();
		var topLeftBox = await topLeftCell.BoundingBoxAsync();
		var bottomRightBox = await bottomRightCell.BoundingBoxAsync();
		tileBox.Should().NotBeNull();
		topLeftBox.Should().NotBeNull();
		bottomRightBox.Should().NotBeNull();

		Math.Abs(tileBox!.X - topLeftBox!.X).Should().BeLessThan(20,
			$"{widgetTestId}'s left edge should align with column {x}");
		Math.Abs(tileBox.Y - topLeftBox.Y).Should().BeLessThan(20,
			$"{widgetTestId}'s top edge should align with row {y}");
		Math.Abs(tileBox.X + tileBox.Width - (bottomRightBox!.X + bottomRightBox.Width)).Should().BeLessThan(20,
			$"{widgetTestId}'s right edge should align with the end of column {x + width - 1}");
		Math.Abs(tileBox.Y + tileBox.Height - (bottomRightBox.Y + bottomRightBox.Height)).Should().BeLessThan(20,
			$"{widgetTestId}'s bottom edge should align with the end of row {y + height - 1}");
	}

	private static int GridCellIndex(int col, int row)
	{
		const int gridColumns = 8;
		return (row - 1) * gridColumns + (col - 1);
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

	private async Task<string> GetAccessTokenAsync()
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in localStorage after login");
		return token!;
	}
}
