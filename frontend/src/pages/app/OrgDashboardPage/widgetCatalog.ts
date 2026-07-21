// Widget keys mirror the backend's DashboardWidgetKey enum exactly (see
// backend/src/Domain/Organizations/DashboardWidgetKey.cs) - they travel
// to/from the API as these same string literals.
export type WidgetKey =
	| "ToDo"
	| "UpcomingOpportunities"
	| "Calendar"
	| "Settings"
	| "CreateOpportunity"
	| "QuickCheckIn"
	| "SettingsIcon";

// A widget's own layout variant, driven entirely by how many of the 8 grid
// columns the auto-fit packer below actually gave it - not a size the
// organizer picks (#771 follow-up review feedback removed the manual
// resize slider in favor of this).
export type WidgetSizeClass = "compact" | "medium" | "full";

interface WidgetCatalogEntry {
	titleKey: string;
	// Column footprint on the 8-column grid - a widget always takes up to
	// maxCols of whatever room is left in the current row, down to minCols
	// if that's all that's left (see packWidgets below).
	minCols: number;
	maxCols: number;
	// Nominal row-unit height, used for packing (how tall a "shelf" is) and
	// the green cell backdrop's row count. The actual CSS row uses
	// minmax(unit, auto), so real content taller than this estimate still
	// fits without clipping - this is a placement estimate, not a hard cap.
	rows: number;
}

export const WIDGET_CATALOG: Record<WidgetKey, WidgetCatalogEntry> = {
	CreateOpportunity: {
		titleKey: "orgDashboard.createOpportunityWidgetTitle",
		minCols: 2,
		maxCols: 4,
		rows: 2,
	},
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		minCols: 3,
		maxCols: 8,
		rows: 2,
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		minCols: 3,
		maxCols: 8,
		rows: 3,
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		minCols: 3,
		maxCols: 8,
		rows: 6,
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		minCols: 3,
		maxCols: 8,
		rows: 2,
	},
	QuickCheckIn: {
		titleKey: "orgDashboard.quickCheckInWidgetTitle",
		minCols: 2,
		maxCols: 4,
		rows: 2,
	},
	SettingsIcon: {
		titleKey: "orgDashboard.settingsIconWidgetTitle",
		// Fixed - min equals max, so it's never anything but compact.
		minCols: 2,
		maxCols: 2,
		rows: 1,
	},
};

export const WIDGET_KEYS = Object.keys(WIDGET_CATALOG) as WidgetKey[];

// Layout applied when an organizer hasn't customized their dashboard yet -
// matches the previous hardcoded dashboard (ToDo+Upcoming side by side,
// Calendar and Settings full-width below), plus the former standalone
// "+ Create Opportunity" button folded into its own widget tile. Order
// only - sizing is always auto-fit, never stored.
export const DEFAULT_LAYOUT: WidgetKey[] = [
	"CreateOpportunity",
	"ToDo",
	"UpcomingOpportunities",
	"Calendar",
	"Settings",
];

const GRID_COLUMNS = 8;

export interface PackedWidget {
	widgetKey: WidgetKey;
	col: number; // 1-based grid-column-start
	colSpan: number;
	row: number; // 1-based grid-row-start
	rowSpan: number;
}

// True skyline/masonry packer (not a row-shelf packer): tracks the next
// free row per column independently, so one column can run taller than its
// neighbor - e.g. a wide widget spanning two rows next to two narrower
// widgets stacked in the remaining columns. A shelf packer can never
// produce that (it always advances every column to the same row together),
// which is exactly the arrangement organizers asked for (#762 follow-up
// feedback - "widget 3 spans two rows while widgets 1 and 2 stack next to
// it"). For each widget, every (start column, span width) combination
// between its minCols and maxCols is scored by the row it would land on
// (the tallest already-occupied column under that span) - the lowest row
// wins, so a widget only wraps to a new row when nothing shallower is
// available. Spans are tried widest-first, so a narrower span only
// displaces the current best by finding a strictly lower row than any
// wider span could - on an equal row, whichever span got there first (the
// widest one) keeps it, still giving "take as much room as is left"; ties
// at that same span are broken by the leftmost column. Every catalog
// minCols is <= GRID_COLUMNS, so a placement always exists - there's no
// "doesn't fit" failure mode this needs to report back.
export function packWidgets(order: WidgetKey[]): {
	placed: PackedWidget[];
	totalRows: number;
} {
	// heights[c] = next free row (1-based) in column c.
	const heights = new Array<number>(GRID_COLUMNS).fill(1);
	const placed: PackedWidget[] = [];

	for (const widgetKey of order) {
		const entry = WIDGET_CATALOG[widgetKey];
		let bestCol = 0;
		let bestSpan = entry.minCols;
		let bestRow = Infinity;

		for (let span = entry.maxCols; span >= entry.minCols; span--) {
			for (let col = 0; col + span <= GRID_COLUMNS; col++) {
				let row = 1;
				for (let c = col; c < col + span; c++) {
					row = Math.max(row, heights[c]);
				}
				const better =
					row < bestRow ||
					(row === bestRow && span === bestSpan && col < bestCol);
				if (better) {
					bestRow = row;
					bestSpan = span;
					bestCol = col;
				}
			}
		}

		placed.push({
			widgetKey,
			col: bestCol + 1,
			colSpan: bestSpan,
			row: bestRow,
			rowSpan: entry.rows,
		});
		for (let c = bestCol; c < bestCol + bestSpan; c++) {
			heights[c] = bestRow + entry.rows;
		}
	}

	const totalRows = Math.max(...heights) - 1;
	return { placed, totalRows };
}

export function classifyWidth(colSpan: number): WidgetSizeClass {
	if (colSpan <= 3) return "compact";
	if (colSpan <= 5) return "medium";
	return "full";
}

// Defends against a saved layout referencing a widget key this build no
// longer knows about (e.g. a widget retired in a later release) - drops
// unrecognized keys entirely rather than trusting the API response as-is.
export function sanitizeWidgetKey(key: string): WidgetKey | null {
	return key in WIDGET_CATALOG ? (key as WidgetKey) : null;
}
