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

// Shelf/skyline packer: each widget greedily takes up to its own maxCols of
// whatever room is left in the current shelf (row band), wrapping to a
// fresh shelf once even its minCols doesn't fit - this is what gives
// "takes as much space as is left" without a manual size picker. Every
// catalog minCols is <= GRID_COLUMNS, so a widget always fits somewhere
// (worst case, its own full-width shelf) - there's no "doesn't fit"
// failure mode this needs to report back.
export function packWidgets(order: WidgetKey[]): {
	placed: PackedWidget[];
	totalRows: number;
} {
	let col = 0;
	let rowStart = 1;
	let shelfHeight = 0;
	const placed: PackedWidget[] = [];

	for (const widgetKey of order) {
		const entry = WIDGET_CATALOG[widgetKey];
		if (col > 0 && entry.minCols > GRID_COLUMNS - col) {
			rowStart += shelfHeight;
			col = 0;
			shelfHeight = 0;
		}
		const colSpan = Math.min(entry.maxCols, GRID_COLUMNS - col);
		placed.push({
			widgetKey,
			col: col + 1,
			colSpan,
			row: rowStart,
			rowSpan: entry.rows,
		});
		col += colSpan;
		shelfHeight = Math.max(shelfHeight, entry.rows);
	}

	return { placed, totalRows: rowStart + shelfHeight - 1 };
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
