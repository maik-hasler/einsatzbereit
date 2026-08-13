import { describe, it, expect } from "vitest";
import {
	DEFAULT_LAYOUT,
	WIDGET_CATALOG,
	isValidPlacement,
	type PlacedWidget,
} from "./widgetCatalog";

// #1795: the Calendar shipped at 6 rows tall, which at 1440px is ~900px of
// the first screen an organizer opens handed to a month grid that usually
// holds a couple of event bars. These guard the shape of the default the
// issue settled on - and, just as importantly, that shrinking a widget in
// DEFAULT_LAYOUT doesn't leave the stack below it floating over a gap.
describe("DEFAULT_LAYOUT", () => {
	it("gives the Calendar a footprint proportionate to its content", () => {
		const calendar = DEFAULT_LAYOUT.find((w) => w.widgetKey === "Calendar");
		expect(calendar).toBeDefined();
		expect(calendar?.height).toBe(4);
	});

	// Widths deliberately differ - UpcomingOpportunities is stretched to the
	// full 8 columns here while its catalog default is 4 - but heights are the
	// documented invariant: a widget taller than it needs to be leaves dead
	// space inside its own card.
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
		// Rows are only ever fully occupied or fully free in this layout, so
		// "no gaps" is just: the set of occupied rows is 1..bottom with nothing
		// missing, and no cell is claimed twice.
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
