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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEdit", pinnedOrgId!.Value);

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("quick-action-cancel")).ToHaveCountAsync(0);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("quick-action-cancel")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToHaveCountAsync(0);

		// Edit mode disables the widgets' own content (see EditableWidgetTile's
		// `inert` wrapper) - the move/remove toolbar is still usable.
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")
				.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" }))
			.ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-cancel").ClickAsync();

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("quick-action-cancel")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task RemovingAWidget_AndSaving_PersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRemove", pinnedOrgId!.Value);

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToHaveCountAsync(0);

		await Page.GetByTestId("quick-action-save").ClickAsync();

		// Save exits edit mode back to "Edit" once the PUT resolves.
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToHaveCountAsync(0);

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToHaveCountAsync(0,
			new() { Timeout = 10_000 });

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashAdd", pinnedOrgId!.Value);

		// QuickCheckIn isn't part of the default layout (see DEFAULT_LAYOUT in
		// widgetCatalog.ts), so a fresh org never has it yet.
		await Expect(Page.GetByTestId("widget-tile-QuickCheckIn")).ToHaveCountAsync(0);

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashCancel", pinnedOrgId!.Value);

		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToHaveCountAsync(0);

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashBackdrop", pinnedOrgId!.Value);

		await Expect(Page.Locator("input[type='range']")).ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("dashboard-grid-guide-cell")).ToHaveCountAsync(0);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await Expect(Page.Locator("input[type='range']")).ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("dashboard-grid-guide-cell")).Not.ToHaveCountAsync(0);

		await Page.GetByTestId("quick-action-cancel").ClickAsync();

		await Expect(Page.GetByTestId("dashboard-grid-guide-cell")).ToHaveCountAsync(0);
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlay", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var backdropCells = Page.GetByTestId("dashboard-grid-guide-cell");
		await Expect(backdropCells.First).ToBeVisibleAsync();
		var widgetTile = Page.GetByTestId("widget-tile-CreateOpportunity");
		await Expect(widgetTile).ToBeVisibleAsync();

		// CreateOpportunity is placed at (x=1, y=1) in DEFAULT_LAYOUT - its
		// tile's top edge should sit right at (accounting for the backdrop's
		// `-m-1` bleed) the very first backdrop cell's top edge. Both boxes
		// read in a single EvaluateAsync call rather than two separate
		// BoundingBoxAsync round trips, so nothing can shift layout between
		// reading the cell's box and the widget's box.
		var yDelta = 0d;
		await PollUntilAsync(async () =>
		{
			yDelta = await Page.EvaluateAsync<double>(
				"""
				() => {
					const cell = document.querySelector('[data-testid="dashboard-grid-guide-cell"]');
					const widget = document.querySelector('[data-testid="widget-tile-CreateOpportunity"]');
					return Math.abs(widget.getBoundingClientRect().y - cell.getBoundingClientRect().y);
				}
				""");
			return yDelta < 20;
		}, () => "the first widget should render at the top of the grid, aligned with the first backdrop "
			+ $"cell - not pushed below the entire backdrop into a separate stack of cards "
			+ $"(last observed delta: {yDelta}px, must be <20px)");
	}

	[Test]
	public async Task MouseCornerPlacement_MovingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashMousePlace", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1)
		// - see placeNewWidget in widgetCatalog.ts - making the grid-cell
		// index math below trivial (an 8-column-wide, 1-row-tall grid).
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-Settings");
		await Expect(tile).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();

		// Settings starts at (x=1, y=1, width=8, height=1) - click column 2,
		// row 1 as the first corner, then column 5, row 2 as the second, to
		// move+resize it to (x=2, y=1, width=4, height=2).
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
	public async Task CornerPlacement_HoveringAGuideCell_UpdatesThePlacementBanner()
	{
		// #1402: the grid-guide backdrop cells' click/hover handling moved from
		// one onClick/onPointerEnter pair per cell (up to 832 of them) to a
		// single delegated pair on the grid container, which reads the hovered
		// cell's col/row off data-col/data-row attributes via closest() rather
		// than a per-cell closure. This is the regression guard for the hover
		// half of that refactor: hovering a cell that was never clicked must
		// still move the live placement cursor, purely via a real bubbled
		// pointerover - the click-driven half is already exercised end to end
		// by every other placement test in this file (each click bubbles
		// through the same delegated container handler).
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashHoverBanner", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=1) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToContainTextAsync("Column 1, row 1");

		// Hover a distant cell without clicking it - a real mouse move, so the
		// resulting pointerover must bubble to the container's delegated
		// handler exactly like a real drag/hover would.
		await HoverGridCellAsync(col: 8, row: 3);

		await Expect(Page.GetByTestId("dashboard-placement-status")).ToContainTextAsync("Column 8, row 3");

		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task PointerDrag_WithManyRapidIntermediateMoves_StillCommitsTheFinalPosition()
	{
		// #1402: dragging now batches its live preview updates to at most one
		// per animation frame (see useWidgetPlacement's rAF throttle) instead
		// of one per raw pointermove - a high-polling-rate mouse/trackpad can
		// fire pointermove well past the screen's refresh rate. The widget's
		// actual committed rect on release must still reflect the very last
		// pointer position even though most of the intermediate ones were
		// coalesced away, so this drags through many more intermediate steps
		// than PointerDrag_MovingAWidget_... above to make sure nothing about
		// that batching drops or staggers the final commit.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRapidDrag", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=1) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" }))
			.ToBeVisibleAsync();
		var (colPx, startX, startY) = await GetGripDragStartAsync(tile, "Move or resize Needs Your Attention");

		// Drag four grid columns to the right (x=1 -> x=5) over many small
		// steps, well beyond what a single animation frame could each get its
		// own render for.
		await Page.Mouse.MoveAsync(startX, startY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(startX + colPx * 4, startY, new() { Steps = 40 });
		await Page.Mouse.UpAsync();

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 5, y: 1, width: 4, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 5, y: 1, width: 4, height: 1);
	}

	[Test]
	public async Task PointerDrag_MovingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		// #16: a real press-and-drag on the grip button, distinct from the
		// click-click-click flow covered above - moves the widget live under
		// the pointer and commits on release, with no corner-picking banner
		// involved at all.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashPointerDrag", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=1) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" }))
			.ToBeVisibleAsync();
		var (colPx, startX, startY) = await GetGripDragStartAsync(tile, "Move or resize Needs Your Attention");

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
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 3, y: 1, width: 4, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 3, y: 1, width: 4, height: 1);
	}

	[Test]
	public async Task CornerResizeHandle_PointerDrag_ResizesBothAxesTogether_AndPersistsAcrossReload()
	{
		// #830 briefly added dedicated right-edge/bottom-edge handles alongside
		// this corner one, for single-axis resizing - reverted in the #783
		// review round-trip: on top of the existing grip/corner-resize/remove
		// trio, two more permanently-visible controls left too little bare
		// tile surface to grab-and-drag on the smaller widget sizes, which the
		// organizer read as "you added more buttons, I can't move anything
		// else - it's just not working". The corner handle (both axes at once
		// via a real pointer drag, distinct from the click-click-click/
		// keyboard flow covered elsewhere in this file) is the only
		// mouse-driven resize affordance again - this is its regression guard.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashCornerResize", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Add exactly one widget so its placement lands at a known (x=1, y=1,
		// width=4, height=1) - see placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();

		// Drag the corner handle one column right and one row down: width
		// 4 -> 5 and height 1 -> 2 together, from the same single drag.
		var cornerHandle = tile.GetByTestId("widget-resize-handle-corner");
		await Expect(cornerHandle).ToBeVisibleAsync();
		var (colPx, rowPx, startX, startY) = await GetCornerHandleDragStartAsync(tile);

		await Page.Mouse.MoveAsync(startX, startY);
		await Page.Mouse.DownAsync();
		await Page.Mouse.MoveAsync(startX + colPx, startY + rowPx, new() { Steps = 5 });
		await Page.Mouse.UpAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 5, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 5, height: 2);
	}

	[Test]
	public async Task GridCellShape_StaysConsistentAcrossViewportWidths()
	{
		// #783 review feedback (comment #5049781309): "When I move it to a
		// different sized monitor, everything becomes a weird size." The grid
		// used a flat auto-rows-[64px] while column width already scaled with
		// the viewport (grid-cols-8's 1fr tracks) - a widget's on-screen shape
		// (row height relative to column width) would warp between a wide
		// monitor and a narrower one even though its stored cell width/height
		// never changed. Row height now tracks the actual rendered column
		// width via a container query (.dashboard-widget-grid in global.css),
		// so a grid cell's aspect ratio should barely move between two very
		// differently-sized viewports, even though its absolute pixel size
		// does.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(1400, 900);
		await CreateOrganizationAsync("Visual DashViewportShape", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		var cell = Page.GetByTestId("dashboard-grid-guide-cell").First;
		await Expect(cell).ToBeVisibleAsync();

		double wideWidth = 0, wideHeight = 0;
		await PollUntilAsync(async () =>
		{
			var box = await cell.EvaluateAsync<double[]>(
				"el => { const r = el.getBoundingClientRect(); return [r.width, r.height]; }");
			wideWidth = box[0];
			wideHeight = box[1];
			return wideWidth > 0;
		}, () => $"grid cell never reported a non-zero width at the wide (1400x900) viewport "
			+ $"(last observed width: {wideWidth}px)");

		await Page.SetViewportSizeAsync(1024, 900);

		// The grid reflows on viewport change - there's no fixed expected
		// value to Expect() against here (the narrow width isn't known in
		// advance), so poll a fresh read of both dimensions in a single
		// EvaluateAsync call each iteration until the reflow has actually
		// landed (width genuinely differs from the wide-viewport sample),
		// rather than reading once right after SetViewportSizeAsync returns.
		double narrowWidth = 0, narrowHeight = 0;
		await PollUntilAsync(async () =>
		{
			var box = await Page.GetByTestId("dashboard-grid-guide-cell").First
				.EvaluateAsync<double[]>("el => { const r = el.getBoundingClientRect(); return [r.width, r.height]; }");
			narrowWidth = box[0];
			narrowHeight = box[1];
			return narrowWidth > 0 && Math.Abs(narrowWidth - wideWidth) > 5;
		}, () => $"grid cell width never changed after resizing to the narrow (1024x900) viewport "
			+ $"(wide: {wideWidth}px, last observed narrow: {narrowWidth}px)");

		(wideWidth - narrowWidth).Should().BeGreaterThan(5,
			"column width should actually shrink at the narrower viewport - otherwise this test isn't exercising anything");

		var wideAspect = wideHeight / wideWidth;
		var narrowAspect = narrowHeight / narrowWidth;
		Math.Abs(wideAspect - narrowAspect).Should().BeLessThan(0.15f,
			"a grid cell's shape (row height relative to column width) should stay roughly the same across viewport "
				+ "widths - a fixed row height would keep cells short-and-wide on a wide viewport and "
				+ "square-ish on a narrow one, changing every widget's on-screen proportions between screens");
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashHorizontalCompact", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create Opportunity widget" })
			.ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 4, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 4, height: 1);
	}

	[Test]
	public async Task KeyboardCornerPlacement_ResizingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashKeyboardPlace", pinnedOrgId!.Value);

		// ToDo starts at (x=5, y=1, width=4, height=1) in DEFAULT_LAYOUT.
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" });
		await moveButton.FocusAsync();

		// Enter/Space on the focused button advances the same state machine
		// a mouse click on a grid cell does: first press starts placing
		// (cursor defaults to the widget's current top-left corner, x=5/y=1),
		// second press locks that as the first corner, then ArrowRight and
		// ArrowDown move the cursor to (col=6, row=2) before the third press
		// commits the second corner there - resizing the tile from
		// (width=4, height=1) to (width=2, height=2) while keeping the same
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEscapeCancel", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();
		var styleBefore = await tile.EvaluateAsync<string?>("el => el.getAttribute('style')");

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs Your Attention" });
		await moveButton.FocusAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Escape");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();

		// No fixed expected value to Expect() against up front beyond the
		// snapshot captured above - poll a fresh read of the live attribute
		// until it settles back to matching that snapshot (or the timeout
		// proves it never does), rather than reading it once right after
		// Escape.
		string? styleAfter = null;
		await PollUntilAsync(async () =>
		{
			styleAfter = await tile.EvaluateAsync<string?>("el => el.getAttribute('style')");
			return styleAfter == styleBefore;
		}, () => "Escape must cancel the in-progress placement without changing the widget's stored "
			+ $"position (before: \"{styleBefore}\", last observed after: \"{styleAfter}\")");
	}

	[Test]
	public async Task OverlappingPlacement_DisplacesTheOtherWidgetDownward_InsteadOfBeingRejected()
	{
		// #18: an overlapping placement used to be rejected outright - it now
		// pushes whatever's in the way straight down instead (then closes any
		// gap that leaves further up, see #14's compaction), and persists
		// that displacement across a reload just like any other placement.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlapPush", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		// Settings lands at (x=1, y=1, width=8, height=1); ToDo is added next
		// and lands right below it at (x=1, y=2, width=4, height=1) - see
		// placeNewWidget in widgetCatalog.ts.
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();

		// Grow Settings down into ToDo's row (x=1..4, y=1..4) - overlaps
		// ToDo's (x=1..4, y=2..2). ToDo has nowhere else to go but straight
		// down below the grown Settings tile, landing at (x=1..4, y=5..5).
		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 4, row: 4);

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		// Auto-waiting for zero alerts only proves "never appeared" (not
		// "appeared and already auto-dismissed") because AppHost sets
		// VITE_TOAST_LIFETIME_MS=0 for test runs - see runtimeConfig.ts.
		await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
		await AssertWidgetOccupiesCellsAsync("Settings", x: 1, y: 1, width: 4, height: 4);
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 5, width: 4, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("Settings", x: 1, y: 1, width: 4, height: 4);
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 5, width: 4, height: 1);
	}

	[Test]
	public async Task ResizingBelowAWidgetsMinimumSize_IsRejected_WithErrorToast_AndKeepsPreviousPosition()
	{
		// #15/#18: overlap alone no longer rejects a placement, but a widget's
		// restored per-type minimum size still does - there's nowhere to
		// "push" a widget that's shrunk smaller than it can usefully render.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashMinSizeReject", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Calendar").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var calendarTile = Page.GetByTestId("widget-tile-Calendar");
		await Expect(calendarTile).ToBeVisibleAsync();
		var styleBefore = await calendarTile.EvaluateAsync<string?>("el => el.getAttribute('style')");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Calendar" }).ClickAsync();

		// Calendar's minimum is 4x4 (see WIDGET_CATALOG in widgetCatalog.ts) -
		// shrink it to 3x3 (x=1..3, y=1..3), below that floor on both axes.
		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 3, row: 3);

		await Expect(Page.GetByRole(AriaRole.Alert))
			.ToContainTextAsync("doesn't fit");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();

		// No fixed expected value to Expect() against beyond the snapshot
		// captured above - poll a fresh read of the live attribute until it
		// settles back to matching that snapshot.
		string? styleAfter = null;
		await PollUntilAsync(async () =>
		{
			styleAfter = await calendarTile.EvaluateAsync<string?>("el => el.getAttribute('style')");
			return styleAfter == styleBefore;
		}, () => "a rejected placement must leave the widget at its previous position "
			+ $"(before: \"{styleBefore}\", last observed after: \"{styleAfter}\")");
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashEmpty", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Expect(Page.GetByTestId("dashboard-widget-grid")).ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync();

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByTestId("dashboard-empty-state")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToHaveCountAsync(0);

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
		// opportunities to overflow UpcomingOpportunitiesWidget (height=2,
		// MAX_ITEMS=5) and confirms every backdrop row still shares the
		// same rendered height as an unaffected row.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRowHeight", pinnedOrgId!.Value);
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
				participationType = "ScheduledSlots",
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

		// UpcomingOpportunities sits at y=2..3 in DEFAULT_LAYOUT - row 1 (host
		// to CreateOpportunity/ToDo, neither of which grew) is the unaffected
		// baseline; row 2 falls inside the now-overflowing widget's own rows.
		// Both rows' cell heights read in a single EvaluateAsync call rather
		// than two separate locator round trips, so nothing can reflow
		// between sampling the two.
		const int gridColumns = 8;
		var rowHeightDelta = 0d;
		await PollUntilAsync(async () =>
		{
			rowHeightDelta = await Page.EvaluateAsync<double>(
				"""
				([row1Index, row2Index]) => {
					const cells = document.querySelectorAll('[data-testid="dashboard-grid-guide-cell"]');
					const row1 = cells[row1Index].getBoundingClientRect().height;
					const row2 = cells[row2Index].getBoundingClientRect().height;
					return Math.abs(row1 - row2);
				}
				""", new[] { (1 - 1) * gridColumns, (2 - 1) * gridColumns });
			return rowHeightDelta < 2;
		}, () => "every grid row must share the same fixed height, even when a widget's own content "
			+ "(here, 5 published opportunities in a height=2 widget) overflows its allotted rows - "
			+ "that overflow should scroll within the widget's own card, not stretch the shared row band "
			+ $"(last observed delta: {rowHeightDelta}px, must be <2px)");
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
	/// Hovers the grid guide cell at 1-based (col, row) without clicking it -
	/// a real mouse move, so it exercises the same delegated pointerover
	/// handler on the grid container a real drag would (see #1402).
	/// </summary>
	private async Task HoverGridCellAsync(int col, int row)
	{
		await Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(col, row)).HoverAsync();
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

		await Expect(tile).ToBeVisibleAsync();
		await Expect(topLeftCell).ToBeVisibleAsync();
		await Expect(bottomRightCell).ToBeVisibleAsync();

		// All four edge deltas computed together inside a single
		// EvaluateAsync call (rather than three separate BoundingBoxAsync
		// round trips), so nothing can shift layout between reading the
		// tile's box and the two backdrop cells' boxes that describe the
		// same expected placement - this is the shared helper every
		// placement test in this file calls, so fixing it here fixes every
		// caller.
		double leftDelta = 0, topDelta = 0, rightDelta = 0, bottomDelta = 0;
		await PollUntilAsync(async () =>
		{
			var deltas = await tile.EvaluateAsync<double[]>(
				"""
				(el, args) => {
					const cells = document.querySelectorAll('[data-testid="dashboard-grid-guide-cell"]');
					const tileBox = el.getBoundingClientRect();
					const topLeft = cells[args.topLeftIndex].getBoundingClientRect();
					const bottomRight = cells[args.bottomRightIndex].getBoundingClientRect();
					return [
						Math.abs(tileBox.x - topLeft.x),
						Math.abs(tileBox.y - topLeft.y),
						Math.abs((tileBox.x + tileBox.width) - (bottomRight.x + bottomRight.width)),
						Math.abs((tileBox.y + tileBox.height) - (bottomRight.y + bottomRight.height)),
					];
				}
				""",
				new { topLeftIndex = GridCellIndex(x, y), bottomRightIndex = GridCellIndex(x + width - 1, y + height - 1) });
			leftDelta = deltas[0];
			topDelta = deltas[1];
			rightDelta = deltas[2];
			bottomDelta = deltas[3];
			return leftDelta < 20 && topDelta < 20 && rightDelta < 20 && bottomDelta < 20;
		}, () => $"{widgetTestId} should occupy (x={x}, y={y}, width={width}, height={height}) - all "
			+ $"four edges must align with the corresponding backdrop cell within 20px, but last "
			+ $"observed: left={leftDelta}px (column {x}), top={topDelta}px (row {y}), "
			+ $"right={rightDelta}px (end of column {x + width - 1}), "
			+ $"bottom={bottomDelta}px (end of row {y + height - 1})");
	}

	/// <summary>
	/// Reads a widget tile's per-column pixel width and its move-grip
	/// button's center point in a single EvaluateAsync call (rather than two
	/// separate BoundingBoxAsync round trips), so a mouse-drag test's
	/// computed start coordinates describe one consistent layout instant
	/// instead of two samples that could straddle a React commit. Used by
	/// the pointer-drag move tests (as distinct from the click-click-click
	/// corner flow, which never reads geometry directly).
	/// </summary>
	private async Task<(float ColPx, float StartX, float StartY)> GetGripDragStartAsync(
		ILocator tile, string gripAriaLabel)
	{
		float colPx = 0, startX = 0, startY = 0;
		await PollUntilAsync(async () =>
		{
			var geometry = await tile.EvaluateAsync<double[]>(
				"""
				(el, gripLabel) => {
					const tileRect = el.getBoundingClientRect();
					const grip = el.querySelector(`button[aria-label="${gripLabel}"]`);
					if (!grip || tileRect.width <= 0) return [0, 0, 0, 0];
					const gripRect = grip.getBoundingClientRect();
					return [tileRect.width, gripRect.x + gripRect.width / 2, gripRect.y + gripRect.height / 2, 1];
				}
				""", gripAriaLabel);
			if (geometry[3] == 0)
				return false;
			colPx = (float)(geometry[0] / 4);
			startX = (float)geometry[1];
			startY = (float)geometry[2];
			return true;
		}, () => $"never found a visible \"{gripAriaLabel}\" grip button with a non-zero-width tile "
			+ "to compute a drag start point from");
		return (colPx, startX, startY);
	}

	/// <summary>
	/// Same idea as <see cref="GetGripDragStartAsync"/>, but for the
	/// corner-resize handle test: reads the tile's per-column/per-row pixel
	/// size and the corner handle's center point together in one
	/// EvaluateAsync call.
	/// </summary>
	private async Task<(float ColPx, float RowPx, float StartX, float StartY)> GetCornerHandleDragStartAsync(
		ILocator tile)
	{
		float colPx = 0, rowPx = 0, startX = 0, startY = 0;
		await PollUntilAsync(async () =>
		{
			var geometry = await tile.EvaluateAsync<double[]>(
				"""
				el => {
					const tileRect = el.getBoundingClientRect();
					const handle = el.querySelector('[data-testid="widget-resize-handle-corner"]');
					if (!handle || tileRect.width <= 0 || tileRect.height <= 0) return [0, 0, 0, 0, 0];
					const handleRect = handle.getBoundingClientRect();
					return [
						tileRect.width, tileRect.height,
						handleRect.x + handleRect.width / 2, handleRect.y + handleRect.height / 2,
						1,
					];
				}
				""");
			if (geometry[4] == 0)
				return false;
			colPx = (float)(geometry[0] / 4);
			rowPx = (float)(geometry[1] / 1);
			startX = (float)geometry[2];
			startY = (float)geometry[3];
			return true;
		}, () => "never found a visible corner-resize handle with a non-zero-size tile "
			+ "to compute a drag start point from");
		return (colPx, rowPx, startX, startY);
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
			// Per-widget control flow, not a test-precondition skip: if
			// DEFAULT_LAYOUT (widgetCatalog.ts) ever drops one of these five
			// widgets, there's simply nothing to remove for it here - move on
			// to the next one instead of aborting every test that calls this
			// helper (a bare Skip.When here would do exactly that, since it
			// skips the whole test, not just this loop iteration).
			var tile = Page.GetByTestId($"widget-tile-{testId}");
			if (await tile.CountAsync() == 0)
				continue;
			await tile
				.GetByRole(AriaRole.Button, new() { Name = $"Remove {widgetTitle} widget" })
				.ClickAsync();
		}
	}

	private async Task CreateOrganizationAsync(string namePrefix, Guid organizationId)
	{
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one) - and guarantees a clean, empty org
		// (default dashboard layout) for deterministic widget assertions.
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, organizationId);
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
