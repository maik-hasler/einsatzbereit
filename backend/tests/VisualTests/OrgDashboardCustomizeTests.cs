using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgDashboardCustomizeTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task DefaultLayout_WidgetTiles_RenderInsideBackdropBounds_NotStackedBelowIt()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlay", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var backdropCells = Page.GetByTestId("dashboard-grid-guide-cell");
		await Expect(backdropCells.First).ToBeVisibleAsync();
		var widgetTile = Page.GetByTestId("widget-tile-CreateOpportunity");
		await Expect(widgetTile).ToBeVisibleAsync();

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

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-Settings");
		await Expect(tile).ToBeVisibleAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();

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
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashHoverBanner", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" }).ClickAsync();
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToContainTextAsync("Column 1, row 1");

		await HoverGridCellAsync(col: 8, row: 3);

		await Expect(Page.GetByTestId("dashboard-placement-status")).ToContainTextAsync("Column 8, row 3");

		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task PointerDrag_WithManyRapidIntermediateMoves_StillCommitsTheFinalPosition()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashRapidDrag", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" }))
			.ToBeVisibleAsync();
		var (colPx, startX, startY) = await GetGripDragStartAsync(tile, "Move or resize Needs your attention");

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
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashPointerDrag", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" }))
			.ToBeVisibleAsync();
		var (colPx, startX, startY) = await GetGripDragStartAsync(tile, "Move or resize Needs your attention");

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
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashCornerResize", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		var tile = Page.GetByTestId("widget-tile-ToDo");
		await Expect(tile).ToBeVisibleAsync();

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
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashHorizontalCompact", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("widget-tile-CreateOpportunity")
			.GetByRole(AriaRole.Button, new() { Name = "Remove Create opportunity widget" })
			.ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 3, height: 1);
		await AssertWidgetOccupiesCellsAsync("VolunteerStats", x: 4, y: 1, width: 2, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 1, y: 1, width: 3, height: 1);
		await AssertWidgetOccupiesCellsAsync("VolunteerStats", x: 4, y: 1, width: 2, height: 1);
	}

	[Test]
	public async Task KeyboardCornerPlacement_ResizingAWidget_UpdatesItsGridPosition_AndPersistsAcrossReload()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashKeyboardPlace", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" });
		await moveButton.FocusAsync();

		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Page.Keyboard.PressAsync("ArrowRight");
		await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Enter");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await AssertWidgetOccupiesCellsAsync("ToDo", x: 4, y: 1, width: 2, height: 2);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await AssertWidgetOccupiesCellsAsync("ToDo", x: 4, y: 1, width: 2, height: 2);
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

		var moveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Needs your attention" });
		await moveButton.FocusAsync();
		await Page.Keyboard.PressAsync("Enter");
		await Expect(Page.GetByTestId("dashboard-placement-status")).ToBeVisibleAsync();
		await Page.Keyboard.PressAsync("ArrowDown");
		await Page.Keyboard.PressAsync("Escape");

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();

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
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashOverlapPush", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await RemoveAllWidgetsAsync();

		await Page.GetByTestId("quick-action-add-widget").ClickAsync();
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await dialog.GetByTestId("add-widget-option-Settings").ClickAsync();
		await dialog.GetByTestId("add-widget-option-ToDo").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Organization" }).ClickAsync();

		await ClickGridCellAsync(col: 1, row: 1);
		await ClickGridCellAsync(col: 4, row: 4);

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();

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
	public async Task WidgetContentOverflow_DoesNotStretchTheSharedGridRow()
	{
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
				titleDe = $"Row Height Opportunity {i} {suffix}",
				descriptionDe = "Created by WidgetContentOverflow_DoesNotStretchTheSharedGridRow",
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

	[Test]
	public async Task MobileViewport_RendersWidgetsInPositionOrder_NotArrayOrder()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual DashMobileOrder", pinnedOrgId!.Value);

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Move or resize Create opportunity" }).ClickAsync();
		await ClickGridCellAsync(col: 1, row: 9);
		await ClickGridCellAsync(col: 4, row: 9);

		await Expect(Page.GetByTestId("dashboard-placement-status")).Not.ToBeVisibleAsync();
		await AssertWidgetOccupiesCellsAsync("CreateOpportunity", x: 1, y: 9, width: 4, height: 1);

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Expect(Page.GetByTestId("widget-tile-CreateOpportunity")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(375, 812);

		string[] tileOrder = [];
		await PollUntilAsync(async () =>
		{
			tileOrder = await Page.EvaluateAsync<string[]>(
				"""
				() => Array.from(document.querySelectorAll('[data-testid^="widget-tile-"]'))
					.map(el => el.getAttribute('data-testid'))
				""");
			return tileOrder.Length > 0 && tileOrder[^1] == "widget-tile-CreateOpportunity";
		}, () => "at mobile width, DOM order should follow each widget's saved position (y, then x) - "
			+ "CreateOpportunity was moved below every other widget and should render last, but the "
			+ $"last observed order was [{string.Join(", ", tileOrder)}]");
	}

	private async Task ClickGridCellAsync(int col, int row)
	{
		await Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(col, row)).ClickAsync();
	}

	private async Task HoverGridCellAsync(int col, int row)
	{
		await Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(col, row)).HoverAsync();
	}

	private async Task AssertWidgetOccupiesCellsAsync(string widgetTestId, int x, int y, int width, int height)
	{
		var tile = Page.GetByTestId($"widget-tile-{widgetTestId}");
		var topLeftCell = Page.GetByTestId("dashboard-grid-guide-cell").Nth(GridCellIndex(x, y));
		var bottomRightCell = Page.GetByTestId("dashboard-grid-guide-cell")
			.Nth(GridCellIndex(x + width - 1, y + height - 1));

		await Expect(tile).ToBeVisibleAsync();
		await Expect(topLeftCell).ToBeVisibleAsync();
		await Expect(bottomRightCell).ToBeVisibleAsync();

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
					// Starts-with, not exact: the grip's aria-label carries a trailing
					// keyboard-alternative hint ("- drag, or press Enter and use arrow
					// keys") after the widget name callers pass in here.
					const grip = el.querySelector(`button[aria-label^="${gripLabel}"]`);
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
			("CreateOpportunity", "Create opportunity"),
			("ToDo", "Needs your attention"),
			("VolunteerStats", "Volunteers"),
			("UpcomingOpportunities", "Upcoming opportunities"),
			("Calendar", "Calendar"),
			("Settings", "Organization"),
		})
		{
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
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");
		return token!;
	}
}
