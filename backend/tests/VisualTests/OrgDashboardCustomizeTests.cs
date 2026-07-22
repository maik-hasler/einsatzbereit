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
	public async Task AutoFitGrid_HasNoManualSizeControls_AndShowsGreenBackdropOnlyWhileEditing()
	{
		// #771 follow-up review feedback replaced manual sizing (first a
		// "Small"/"Medium"/"Large" cycle button, then a resize slider) with
		// fully automatic column/row placement - covers that no manual size
		// control exists anymore, and that the green cell backdrop (showing
		// the underlying 8-column grid) only renders while editing.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashAutoFit");

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
	public async Task AutoFitGrid_WidgetTiles_RenderInsideBackdropBounds_NotStackedBelowIt()
	{
		// Regression guard for the #762 follow-up feedback bug: real widget
		// tiles fell out of the CSS grid's auto-placement entirely (rendered
		// as a separate stack of cards below the whole green backdrop)
		// because they only carried a `span N` gridColumn/gridRow with no
		// explicit start line, while the green backdrop cells claim every
		// single cell of the grid explicitly - leaving no auto-placement
		// room left for the widgets to land in. Confirms the fix (explicit
		// `col / span N` placement using the same coordinates the packer
		// already gave the backdrop) by checking a widget tile's top edge
		// lands within the backdrop's own vertical bounds, not far below it.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlay");

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var backdropCells = Page.GetByTestId("dashboard-grid-guide-cell");
		await backdropCells.First.WaitForAsync();
		var firstCellBox = await backdropCells.First.BoundingBoxAsync();
		var lastCellBox = await backdropCells.Last.BoundingBoxAsync();
		firstCellBox.Should().NotBeNull();
		lastCellBox.Should().NotBeNull();

		// CreateOpportunity is the first widget in the default layout, so the
		// packer places it at row 1 - its tile's top edge should sit right at
		// (accounting for the backdrop's `-m-1` bleed) the very first
		// backdrop cell's top edge. Under the bug, the tile was pushed
		// hundreds of pixels below the backdrop's very last cell instead.
		var widgetBox = await Page.GetByTestId("widget-tile-CreateOpportunity").BoundingBoxAsync();
		widgetBox.Should().NotBeNull();

		Math.Abs(widgetBox!.Y - firstCellBox!.Y).Should().BeLessThan(20,
			"the first widget should render at the top of the grid, aligned with the first backdrop "
				+ "cell - not pushed below the entire backdrop into a separate stack of cards");
		widgetBox.Y.Should().BeLessThan(lastCellBox!.Y + lastCellBox.Height,
			"the widget tile must render within the backdrop's own bounds, not below all of it");
	}

	[Test]
	public async Task KeyboardDragReorder_SwapsWidgetWithLeftNeighbor_AndPersistsAcrossReload()
	{
		// Covers the keyboard-accessible reorder path end to end: the hidden
		// grip button (see EditableWidgetTile) is a real focusable element
		// carrying dnd-kit's KeyboardSensor listeners, but nothing previously
		// drove an actual reorder through it - so a handleDragOver/arrayMove
		// regression could ship undetected.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashKeyboardReorder");

		// The default layout's packer places CreateOpportunity and ToDo side
		// by side in the first row (see packWidgets in widgetCatalog.ts) - a
		// keyboard drag of ToDo one step left should swap the two.
		(await GetWidgetOrderAsync()).Should().Equal(
		[
			"widget-tile-CreateOpportunity",
			"widget-tile-ToDo",
			"widget-tile-UpcomingOpportunities",
			"widget-tile-Calendar",
			"widget-tile-Settings",
		]);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var todoGrip = Page.GetByRole(AriaRole.Button, new() { Name = "Drag Needs Your Attention to reorder" });
		await todoGrip.FocusAsync();
		await Page.Keyboard.PressAsync("Space");

		// A single ArrowLeft right after grabbing can race dnd-kit's
		// post-grab rect measurement - a real user always leaves a natural
		// gap between grabbing and steering that Playwright's instant
		// keypress doesn't, and a keypress that lands before measurement
		// finishes is silently swallowed by handleDragOver's over===null
		// bail-out with nothing left to retry it later. Poll the key instead
		// of trusting one press to land; once the swap has actually
		// happened, further presses are no-ops (ToDo has no more left
		// neighbor to swap with).
		var firstTile = Page.Locator("[data-testid^='widget-tile-']").First;
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (await firstTile.GetAttributeAsync("data-testid") != "widget-tile-ToDo")
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException(
					"Keyboard ArrowLeft never reordered ToDo to the front of the widget grid.");
			await Page.Keyboard.PressAsync("ArrowLeft");
			await Page.WaitForTimeoutAsync(200);
		}

		await Page.Keyboard.PressAsync("Space");

		var afterDrag = await GetWidgetOrderAsync();
		afterDrag.Should().Equal(
		[
			"widget-tile-ToDo",
			"widget-tile-CreateOpportunity",
			"widget-tile-UpcomingOpportunities",
			"widget-tile-Calendar",
			"widget-tile-Settings",
		]);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		(await GetWidgetOrderAsync()).Should().Equal(afterDrag,
			"the keyboard-reordered layout must persist across reload, not just live in local drag state");
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

	private async Task<List<string>> GetWidgetOrderAsync()
	{
		var tiles = await Page.Locator("[data-testid^='widget-tile-']").AllAsync();
		var testIds = new List<string>();
		foreach (var tile in tiles)
			testIds.Add(await tile.GetAttributeAsync("data-testid") ?? "");
		return testIds;
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
