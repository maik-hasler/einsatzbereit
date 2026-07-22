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
	// dashboard (see placeNewWidget below) - just a sane starting point, not
	// a constraint beyond the floor minWidth/minHeight set below. The
	// organizer immediately owns the exact bounding box via corner-to-corner
	// placement (#782) and can resize it to anything that fits on the grid.
	defaultWidth: number;
	defaultHeight: number;
	// Smallest size this widget can still usefully render at - e.g. the
	// Calendar needs real room for its month/week grid, while the icon-only
	// SettingsIcon shortcut is happy at its own tiny default. Enforced by
	// isValidPlacement, so every placement path (mouse drag, keyboard,
	// click-click-click) rejects a resize below it the same way.
	minWidth: number;
	minHeight: number;
}

export const WIDGET_CATALOG: Record<WidgetKey, WidgetCatalogEntry> = {
	CreateOpportunity: {
		titleKey: "orgDashboard.createOpportunityWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 3,
		minWidth: 2,
		minHeight: 2,
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 6,
		minWidth: 4,
		minHeight: 4,
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 2,
		minWidth: 3,
		minHeight: 2,
	},
	QuickCheckIn: {
		titleKey: "orgDashboard.quickCheckInWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
	SettingsIcon: {
		titleKey: "orgDashboard.settingsIconWidgetTitle",
		defaultWidth: 2,
		defaultHeight: 1,
		minWidth: 1,
		minHeight: 1,
	},
};

export const WIDGET_KEYS = Object.keys(WIDGET_CATALOG) as WidgetKey[];

// Fixed column count for the dashboard grid - mirrors the backend's
// DashboardGrid.Columns (Domain/Organizations/DashboardGrid.cs). Rows grow
// downward as widgets are placed further down, but still need a real
// ceiling - see GRID_MAX_ROWS.
export const GRID_COLUMNS = 8;

// Mirrors the backend's DashboardGrid.MaxRows - without a ceiling, a
// placement at an astronomically large Y (crafted request, or just a wild
// arrow-key hold) would make the grid-guide-cell backdrop below try to
// render that many rows and hang the edit-mode UI.
export const GRID_MAX_ROWS = 100;

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

// True if `rect` fits on the grid at all: on-grid horizontally, within the
// row ceiling, and at least its widget type's minimum size (#15's restored
// per-widget minimums) - the hard constraints a placement can never be saved
// with, no matter what else is on the grid. Overlapping another widget is
// deliberately NOT checked here (#18): committing an overlapping placement
// displaces whatever is in the way instead of being rejected - see
// resolveOverlaps.
export function isValidPlacement(rect: PlacedWidget): boolean {
	const entry = WIDGET_CATALOG[rect.widgetKey];
	if (rect.x < 1 || rect.y < 1) return false;
	if (rect.width < entry.minWidth || rect.height < entry.minHeight)
		return false;
	if (rect.x + rect.width - 1 > GRID_COLUMNS) return false;
	if (rect.y + rect.height - 1 > GRID_MAX_ROWS) return false;
	return true;
}

// Displaces every widget that overlaps `rect` straight down below it (#18:
// an overlapping placement is no longer rejected, it pushes what's in the
// way out of it instead). Loops to a fixed point rather than a single pass -
// pushing widget A down can newly overlap widget B, which then needs pushing
// too, and so on down the stack. `others` must not itself contain `rect`
// (callers pass the rest of the layout).
export function resolveOverlaps(
	rect: PlacedWidget,
	others: PlacedWidget[],
): PlacedWidget[] {
	const result = others.map((widget) => ({ ...widget }));
	let changed = true;
	while (changed) {
		changed = false;
		for (let i = 0; i < result.length; i++) {
			const blockers = [rect, ...result.filter((_, j) => j !== i)];
			for (const blocker of blockers) {
				if (
					rectsOverlap(result[i], blocker) &&
					blocker.y + blocker.height > result[i].y
				) {
					result[i] = { ...result[i], y: blocker.y + blocker.height };
					changed = true;
				}
			}
		}
	}
	return result;
}

// Slides each of `movable` up as far as it can go without overlapping
// anything already settled above it - starting from `obstacles`, which are
// never themselves moved or reordered. Processes `movable` top-to-bottom,
// then left-to-right, so each one only ever lands against widgets (obstacle
// or already-settled movable) that have already reached their own final
// resting position.
function compactAgainstObstacles(
	movable: PlacedWidget[],
	obstacles: PlacedWidget[],
): PlacedWidget[] {
	const sorted = [...movable].sort((a, b) => a.y - b.y || a.x - b.x);
	const settled: PlacedWidget[] = [...obstacles];
	const result: PlacedWidget[] = [];
	for (const widget of sorted) {
		let bestY = widget.y;
		for (let y = widget.y - 1; y >= 1; y--) {
			const candidate = { ...widget, y };
			if (settled.some((s) => rectsOverlap(candidate, s))) break;
			bestY = y;
		}
		const placed = { ...widget, y: bestY };
		settled.push(placed);
		result.push(placed);
	}
	return result;
}

// Slides every widget up as far as it can go without overlapping another
// (#14: gaps left by a removal or a newly added widget get closed instead
// of sitting empty forever). Used where there's no single widget whose
// exact position must be preserved - see settlePlacement below for the
// commit path, which does have one.
export function compactLayout(widgets: PlacedWidget[]): PlacedWidget[] {
	return compactAgainstObstacles(widgets, []);
}

// The full result of committing `rect` (an explicit organizer placement,
// #782/#16/#18): `rect` lands exactly where the organizer put it - never
// nudged by compaction, which would silently override the one guarantee
// corner-to-corner placement makes - while `others` first gets whatever's
// overlapping it pushed out of the way (resolveOverlaps), then closes up
// around `rect` and each other (compactAgainstObstacles) so that
// displacement doesn't leave a gap of its own further up.
export function settlePlacement(
	rect: PlacedWidget,
	others: PlacedWidget[],
): PlacedWidget[] {
	const pushed = resolveOverlaps(rect, others);
	return [rect, ...compactAgainstObstacles(pushed, [rect])];
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
