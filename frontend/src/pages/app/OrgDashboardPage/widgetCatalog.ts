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

// A widget's own layout variant, driven by how many of the 8 grid columns
// the organizer's own placement gives it (see classifyWidth below).
export type WidgetSizeClass = "compact" | "medium" | "full";

interface WidgetCatalogEntry {
	titleKey: string;
	// Starting column/row span offered when a widget is first added to the
	// dashboard (see placeNewWidget in index.tsx) - just a sane starting
	// point, not a constraint. The organizer immediately owns the exact
	// bounding box via corner-to-corner placement (#782) and can resize it
	// to anything that fits on the grid and doesn't overlap another widget.
	defaultWidth: number;
	defaultHeight: number;
}

export const WIDGET_CATALOG: Record<WidgetKey, WidgetCatalogEntry> = {
	CreateOpportunity: {
		titleKey: "orgDashboard.createOpportunityWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
	},
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 3,
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 6,
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 2,
	},
	QuickCheckIn: {
		titleKey: "orgDashboard.quickCheckInWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
	},
	SettingsIcon: {
		titleKey: "orgDashboard.settingsIconWidgetTitle",
		defaultWidth: 2,
		defaultHeight: 1,
	},
};

export const WIDGET_KEYS = Object.keys(WIDGET_CATALOG) as WidgetKey[];

// Fixed column count for the dashboard grid - mirrors the backend's
// DashboardGrid.Columns (Domain/Organizations/DashboardGrid.cs). Rows are
// unbounded; the grid simply grows downward as widgets are placed further
// down.
export const GRID_COLUMNS = 8;

// A widget's explicit, organizer-drawn bounding box on the grid - 1-based
// grid-cell coordinates and cell spans, exactly as stored by the backend
// (#782). Replaces the automatic skyline packer that used to derive
// col/colSpan/row/rowSpan purely from display order.
export interface PlacedWidget {
	widgetKey: WidgetKey;
	x: number;
	y: number;
	width: number;
	height: number;
}

// Layout applied when an organizer hasn't customized their dashboard yet -
// matches the arrangement the former auto-fit packer used to produce for
// this same widget order (ToDo+CreateOpportunity side by side, then Upcoming
// Opportunities, Calendar and Settings each full-width below), now stored as
// explicit coordinates instead of being recomputed at render time.
export const DEFAULT_LAYOUT: PlacedWidget[] = [
	{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 4, height: 2 },
	{ widgetKey: "ToDo", x: 5, y: 1, width: 4, height: 2 },
	{ widgetKey: "UpcomingOpportunities", x: 1, y: 3, width: 8, height: 3 },
	{ widgetKey: "Calendar", x: 1, y: 6, width: 8, height: 6 },
	{ widgetKey: "Settings", x: 1, y: 12, width: 8, height: 2 },
];

export function classifyWidth(width: number): WidgetSizeClass {
	if (width <= 3) return "compact";
	if (width <= 5) return "medium";
	return "full";
}

// Defends against a saved layout referencing a widget key this build no
// longer knows about (e.g. a widget retired in a later release) - drops
// unrecognized keys entirely rather than trusting the API response as-is.
export function sanitizeWidgetKey(key: string): WidgetKey | null {
	return key in WIDGET_CATALOG ? (key as WidgetKey) : null;
}

export function rectsOverlap(a: PlacedWidget, b: PlacedWidget): boolean {
	return (
		a.x < b.x + b.width &&
		b.x < a.x + a.width &&
		a.y < b.y + b.height &&
		b.y < a.y + a.height
	);
}

// True if `rect` fits on the grid horizontally (X + width doesn't run past
// the last column) and doesn't overlap any other widget's placement. `y`
// has no upper bound - the grid can always grow downward.
export function isValidPlacement(
	rect: PlacedWidget,
	others: PlacedWidget[],
): boolean {
	if (rect.x < 1 || rect.y < 1 || rect.width < 1 || rect.height < 1) {
		return false;
	}
	if (rect.x + rect.width - 1 > GRID_COLUMNS) {
		return false;
	}
	return !others.some(
		(other) => other.widgetKey !== rect.widgetKey && rectsOverlap(rect, other),
	);
}

// Appends a newly added widget directly below every widget currently on the
// grid, at its catalog default size - always non-overlapping since nothing
// occupies the grid below the current bottom edge. The organizer then drags
// it into its intended spot via corner-to-corner placement.
export function placeNewWidget(
	widgetKey: WidgetKey,
	existing: PlacedWidget[],
): PlacedWidget {
	const entry = WIDGET_CATALOG[widgetKey];
	const bottom = existing.reduce((max, w) => Math.max(max, w.y + w.height), 1);
	return {
		widgetKey,
		x: 1,
		y: bottom,
		width: entry.defaultWidth,
		height: entry.defaultHeight,
	};
}
