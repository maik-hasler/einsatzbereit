import { describe, it, expect } from "vitest";
import {
	DEFAULT_LAYOUT,
	GRID_COLUMNS,
	WIDGET_CATALOG,
	compactLayout,
	groupIntoRowBands,
	isValidPlacement,
	placeNewWidget,
	settlePlacement,
	sortByPosition,
	type PlacedWidget,
	type WidgetKey,
} from "./widgetCatalog";

function rectsOverlap(a: PlacedWidget, b: PlacedWidget): boolean {
	return (
		a.x < b.x + b.width &&
		b.x < a.x + a.width &&
		a.y < b.y + b.height &&
		b.y < a.y + a.height
	);
}

describe("DEFAULT_LAYOUT", () => {
	it("gives the Calendar a footprint proportionate to its content", () => {
		const calendar = DEFAULT_LAYOUT.find((w) => w.widgetKey === "Calendar");
		expect(calendar).toBeDefined();
		expect(calendar?.height).toBe(4);
	});

	it("keeps every widget at its own catalog default height", () => {
		for (const widget of DEFAULT_LAYOUT) {
			const entry = WIDGET_CATALOG[widget.widgetKey];
			expect({ key: widget.widgetKey, height: widget.height }).toEqual({
				key: widget.widgetKey,
				height: entry.defaultHeight,
			});
		}
	});

	it("places every widget legally on the grid", () => {
		for (const widget of DEFAULT_LAYOUT) {
			expect(isValidPlacement(widget)).toBe(true);
		}
	});

	it("stacks without overlaps or vertical gaps", () => {
		const occupied = new Set<string>();
		let bottom = 0;
		for (const widget of DEFAULT_LAYOUT) {
			for (let y = widget.y; y < widget.y + widget.height; y++) {
				bottom = Math.max(bottom, y);
				for (let x = widget.x; x < widget.x + widget.width; x++) {
					const cell = `${x},${y}`;
					expect(occupied.has(cell)).toBe(false);
					occupied.add(cell);
				}
			}
		}

		const rows = new Set(
			[...occupied].map((cell) => Number(cell.split(",")[1])),
		);
		for (let y = 1; y <= bottom; y++) {
			expect(rows.has(y)).toBe(true);
		}
	});

	it("puts actionable widgets above the Calendar", () => {
		const rowOf = (key: PlacedWidget["widgetKey"]) =>
			DEFAULT_LAYOUT.find((w) => w.widgetKey === key)?.y;
		const calendarRow = rowOf("Calendar");
		expect(calendarRow).toBeDefined();

		for (const key of [
			"CreateOpportunity",
			"ToDo",
			"UpcomingOpportunities",
		] as const) {
			const row = rowOf(key);
			expect(row).toBeDefined();
			expect(row as number).toBeLessThan(calendarRow as number);
		}
	});
});

describe("sortByPosition", () => {
	it("orders widgets top-to-bottom, then left-to-right, regardless of input array order", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "CreateOpportunity", x: 1, y: 9, width: 4, height: 1 },
			{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
			{ widgetKey: "VolunteerStats", x: 7, y: 1, width: 2, height: 1 },
			{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
		];

		expect(sortByPosition(widgets).map((w) => w.widgetKey)).toEqual([
			"ToDo",
			"VolunteerStats",
			"Calendar",
			"CreateOpportunity",
		]);
	});

	it("breaks a tied row left-to-right by x", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "VolunteerStats", x: 7, y: 1, width: 2, height: 1 },
			{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 3, height: 1 },
			{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
		];

		expect(sortByPosition(widgets).map((w) => w.widgetKey)).toEqual([
			"CreateOpportunity",
			"ToDo",
			"VolunteerStats",
		]);
	});

	it("does not mutate the input array", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "Settings", x: 1, y: 8, width: 8, height: 1 },
			{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
		];
		const original = [...widgets];

		sortByPosition(widgets);

		expect(widgets).toEqual(original);
	});

	it("already matches DEFAULT_LAYOUT's own order, since it was authored top-to-bottom", () => {
		expect(sortByPosition(DEFAULT_LAYOUT)).toEqual(DEFAULT_LAYOUT);
	});
});

describe("groupIntoRowBands", () => {
	it("keeps every DEFAULT_LAYOUT band at the full GRID_COLUMNS width", () => {
		for (const band of groupIntoRowBands(DEFAULT_LAYOUT)) {
			expect(band.columns).toBe(GRID_COLUMNS);
		}
	});

	it("merges widgets whose Y ranges overlap into a single band, even indirectly through a taller widget", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "Calendar", x: 1, y: 1, width: 4, height: 4 },
			{ widgetKey: "CreateOpportunity", x: 5, y: 1, width: 2, height: 1 },
			{ widgetKey: "QuickCheckIn", x: 5, y: 3, width: 2, height: 2 },
		];

		const bands = groupIntoRowBands(widgets);

		expect(bands).toHaveLength(1);
		expect(bands[0].startRow).toBe(1);
		expect(bands[0].columns).toBe(6);
		expect(bands[0].widgets).toHaveLength(3);
	});

	it("splits widgets in adjacent but non-overlapping rows into separate bands", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "UpcomingOpportunities", x: 1, y: 1, width: 4, height: 2 },
			{ widgetKey: "VolunteerStats", x: 1, y: 3, width: 4, height: 1 },
		];

		const bands = groupIntoRowBands(widgets);

		expect(bands).toHaveLength(2);
		expect(bands[0]).toMatchObject({ startRow: 1, columns: 4 });
		expect(bands[1]).toMatchObject({ startRow: 3, columns: 4 });
	});

	it("caps a standalone widget's own band at its own reach, not the full grid", () => {
		const widgets: PlacedWidget[] = [
			{ widgetKey: "UpcomingOpportunities", x: 1, y: 5, width: 4, height: 2 },
		];

		const bands = groupIntoRowBands(widgets);

		expect(bands).toEqual([{ startRow: 5, columns: 4, widgets: [widgets[0]] }]);
	});

	it("returns no bands for an empty layout", () => {
		expect(groupIntoRowBands([])).toEqual([]);
	});
});

describe("placeNewWidget", () => {
	it("places into an empty grid at the top-left corner", () => {
		expect(placeNewWidget("VolunteerStats", [])).toEqual({
			widgetKey: "VolunteerStats",
			x: 1,
			y: 1,
			width: WIDGET_CATALOG.VolunteerStats.defaultWidth,
			height: WIDGET_CATALOG.VolunteerStats.defaultHeight,
		});
	});

	it("fills open space beside existing widgets instead of stacking below them", () => {
		const existing: PlacedWidget[] = [
			{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 2, height: 1 },
			{ widgetKey: "Settings", x: 1, y: 2, width: 3, height: 1 },
		];

		const placed = placeNewWidget("ToDo", existing);

		// Row 1, beside what is already there, rather than appended under it.
		// Not column 3: the tile is two rows tall, so starting there would put
		// its lower half through the Settings strip in row 2.
		expect(placed).toEqual({
			widgetKey: "ToDo",
			x: 4,
			y: 1,
			width: WIDGET_CATALOG.ToDo.defaultWidth,
			height: WIDGET_CATALOG.ToDo.defaultHeight,
		});
		for (const widget of existing) {
			expect(rectsOverlap(placed, widget)).toBe(false);
		}
	});

	it("places three widgets added in sequence into separate open cells rather than one shared column", () => {
		let layout: PlacedWidget[] = [
			{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 2, height: 1 },
			{ widgetKey: "Settings", x: 1, y: 2, width: 8, height: 1 },
		];
		const added: WidgetKey[] = [];
		for (const key of [
			"UpcomingOpportunities",
			"VolunteerStats",
			"ToDo",
		] as const) {
			layout = compactLayout([...layout, placeNewWidget(key, layout)]);
			added.push(key);
		}

		const newWidgets = layout.filter((w) => added.includes(w.widgetKey));

		for (let i = 0; i < layout.length; i++) {
			for (let j = i + 1; j < layout.length; j++) {
				expect(rectsOverlap(layout[i], layout[j])).toBe(false);
			}
		}

		const distinctColumns = new Set(newWidgets.map((w) => w.x));
		expect(distinctColumns.size).toBeGreaterThan(1);
	});

	it("falls back to appending below everything once no gap fits", () => {
		const placed = placeNewWidget("QuickCheckIn", DEFAULT_LAYOUT);
		const bottom = DEFAULT_LAYOUT.reduce(
			(max, w) => Math.max(max, w.y + w.height),
			1,
		);

		expect(placed).toEqual({
			widgetKey: "QuickCheckIn",
			x: 1,
			y: bottom,
			width: WIDGET_CATALOG.QuickCheckIn.defaultWidth,
			height: WIDGET_CATALOG.QuickCheckIn.defaultHeight,
		});
	});

	it("never places a widget overlapping an existing one", () => {
		const existing: PlacedWidget[] = [
			{ widgetKey: "Calendar", x: 1, y: 1, width: 8, height: 4 },
			{ widgetKey: "VolunteerStats", x: 1, y: 5, width: 2, height: 1 },
		];

		const placed = placeNewWidget("ToDo", existing);

		for (const widget of existing) {
			expect(rectsOverlap(placed, widget)).toBe(false);
		}
	});
});

describe("settlePlacement", () => {
	// The layout #2322 F1 was reproduced against: a two-widget top row over a
	// stack of full-width tiles, so a displaced widget's own column has no
	// free path back up and compaction alone cannot rescue it.
	const CROWDED_LAYOUT: PlacedWidget[] = [
		{ widgetKey: "ToDo", x: 1, y: 1, width: 3, height: 1 },
		{ widgetKey: "VolunteerStats", x: 4, y: 1, width: 2, height: 1 },
		{ widgetKey: "UpcomingOpportunities", x: 1, y: 2, width: 8, height: 2 },
		{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
		{ widgetKey: "Settings", x: 1, y: 8, width: 8, height: 1 },
		{ widgetKey: "CreateOpportunity", x: 1, y: 9, width: 4, height: 1 },
		{ widgetKey: "QuickCheckIn", x: 5, y: 9, width: 4, height: 2 },
	];

	const settleInto = (rect: PlacedWidget, layout: PlacedWidget[]) =>
		settlePlacement(
			rect,
			layout.filter((w) => w.widgetKey !== rect.widgetKey),
		);

	const positionOf = (layout: PlacedWidget[], key: WidgetKey) =>
		layout.find((w) => w.widgetKey === key);

	it("leaves a displaced neighbour in the row it was already in", () => {
		const settled = settleInto(
			{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
			CROWDED_LAYOUT,
		);

		expect(positionOf(settled, "ToDo")).toMatchObject({ x: 4, y: 1 });
		expect(positionOf(settled, "VolunteerStats")?.y).toBe(1);
	});

	it("keeps every other widget exactly where it was", () => {
		const settled = settleInto(
			{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
			CROWDED_LAYOUT,
		);

		for (const key of [
			"UpcomingOpportunities",
			"Calendar",
			"Settings",
			"CreateOpportunity",
			"QuickCheckIn",
		] as const) {
			expect({ key, ...positionOf(settled, key) }).toEqual({
				key,
				...positionOf(CROWDED_LAYOUT, key),
			});
		}
	});

	// A full-width tile cannot sit beside the widget that displaced it, so it
	// has to drop - but only past the placement, not past the whole board.
	it("keeps a widget that has to drop above the ones already below it", () => {
		const settled = settleInto(
			{ widgetKey: "VolunteerStats", x: 4, y: 1, width: 2, height: 3 },
			CROWDED_LAYOUT,
		);

		const rowOf = (key: WidgetKey) => positionOf(settled, key)?.y ?? Infinity;

		expect(rowOf("UpcomingOpportunities")).toBeGreaterThan(
			rowOf("VolunteerStats"),
		);
		for (const below of ["Calendar", "Settings", "QuickCheckIn"] as const) {
			expect({
				key: below,
				staysBelow: rowOf("UpcomingOpportunities") < rowOf(below),
			}).toEqual({ key: below, staysBelow: true });
		}
	});

	it("never overlaps the widget being placed, or anything else", () => {
		for (const rect of [
			{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
			{ widgetKey: "VolunteerStats", x: 4, y: 1, width: 2, height: 3 },
			{ widgetKey: "Calendar", x: 1, y: 1, width: 8, height: 4 },
			{ widgetKey: "Settings", x: 7, y: 2, width: 2, height: 1 },
		] as PlacedWidget[]) {
			const settled = settleInto(rect, CROWDED_LAYOUT);

			expect(settled.map((w) => w.widgetKey).sort()).toEqual(
				[
					rect.widgetKey,
					...CROWDED_LAYOUT.map((w) => w.widgetKey).filter(
						(key) => key !== rect.widgetKey,
					),
				].sort(),
			);
			for (let i = 0; i < settled.length; i++) {
				for (let j = i + 1; j < settled.length; j++) {
					expect({
						placed: rect.widgetKey,
						pair: [settled[i].widgetKey, settled[j].widgetKey],
						overlaps: rectsOverlap(settled[i], settled[j]),
					}).toMatchObject({ overlaps: false });
				}
			}
		}
	});

	it("keeps the placed widget exactly where it was asked to go", () => {
		const rect: PlacedWidget = {
			widgetKey: "Calendar",
			x: 1,
			y: 1,
			width: 8,
			height: 4,
		};

		expect(positionOf(settleInto(rect, CROWDED_LAYOUT), "Calendar")).toEqual(
			rect,
		);
	});
});
