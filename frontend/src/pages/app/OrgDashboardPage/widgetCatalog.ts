// Widget keys mirror the backend's DashboardWidgetKey enum exactly (see
// backend/src/Domain/Organizations/DashboardWidgetKey.cs) - they travel
// to/from the API as these same string literals.
export type WidgetKey =
	| "ToDo"
	| "VolunteerStats"
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
		defaultHeight: 1,
		minWidth: 2,
		minHeight: 1,
	},
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 1,
		minWidth: 2,
		minHeight: 1,
	},
	VolunteerStats: {
		titleKey: "orgDashboard.volunteerStatsWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 1,
		minWidth: 2,
		minHeight: 1,
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		defaultWidth: 4,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		defaultWidth: 8,
		// 4 rows, not the 6 this started at (#1795): at 1440px a row is
		// ~150px, so 6 rows handed a month grid holding two event bars over
		// 900px of the first screen an organizer opens. 4 rows sits exactly on
		// minHeight below - still comfortably above CalendarWidget's own
		// 400px floor, so the month grid it opens on at full width keeps
		// rendering legibly - and gives back ~300px to the widgets carrying
		// actionable information. The view stays month at this width
		// (defaultViewForSize in CalendarWidget.tsx); the footprint was the
		// problem, not the view.
		defaultHeight: 4,
		minWidth: 4,
		minHeight: 4,
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 1,
		minWidth: 3,
		minHeight: 1,
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
// (#782).
export interface PlacedWidget {
	widgetKey: WidgetKey;
	x: number;
	y: number;
	width: number;
	height: number;
}

// Layout applied when an organizer hasn't customized their dashboard yet:
// CreateOpportunity+ToDo+VolunteerStats across the top row, then
// UpcomingOpportunities, Calendar and Settings each full-width below. Heights
// match each widget's own defaultHeight above so no card leaves dead space,
// and each row starts exactly where the one above it ends so the stack has no
// vertical gaps - widgetCatalog.test.ts asserts both, so shrinking a widget
// here (as #1795 did to the Calendar) can't silently leave a hole below it.
// Widths are free to differ from the catalog defaults and do: the top row's
// three tiles split the 8 columns 3/3/2, and UpcomingOpportunities is
// stretched to the full width.
//
// VolunteerStats (#1780) fits into that existing top row rather than claiming
// a row of its own: it's one number, so a full-width band for it would be
// mostly whitespace, and this way every widget below keeps the row it has
// always started on.
//
// Only organizations that have never customized their dashboard ever see
// this - OrgDashboardPage/index.tsx falls back to it solely when the API
// reports hasCustomLayout: false, and nothing migrates or rewrites a saved
// layout, so editing it leaves every stored layout exactly as it was.
export const DEFAULT_LAYOUT: PlacedWidget[] = [
	{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 3, height: 1 },
	{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
	{ widgetKey: "VolunteerStats", x: 7, y: 1, width: 2, height: 1 },
	{ widgetKey: "UpcomingOpportunities", x: 1, y: 2, width: 8, height: 2 },
	{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
	{ widgetKey: "Settings", x: 1, y: 8, width: 8, height: 1 },
];

// Stable top-to-bottom, then left-to-right ordering by saved grid position -
// what mobile rendering falls back to below the `lg` breakpoint, where the
// grid collapses to a single stacked column with no explicit gridColumn/
// gridRow per tile (see OrgDashboardPage/index.tsx's gridStyle) and DOM
// order becomes the visual order. A layout array's own order can't be
// trusted for that: settlePlacement above always prepends whichever widget
// just moved, regardless of where it actually landed, so array order
// reflects edit history rather than position (#1845).
export function sortByPosition(widgets: PlacedWidget[]): PlacedWidget[] {
	return [...widgets].sort((a, b) => a.y - b.y || a.x - b.x);
}

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

function rectsOverlap(a: PlacedWidget, b: PlacedWidget): boolean {
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
	// A real pointer drag (#16) derives x/y/width/height from a division by
	// a measured pixel size - if that measurement ever races to 0 in a real
	// browser, the result is NaN/Infinity, and every check below is a `<`/`>`
	// comparison that's silently false for both, letting a garbage rect
	// sail through as "valid" and get saved. Reject outright instead of
	// relying on the comparisons to catch it incidentally.
	if (
		!Number.isFinite(rect.x) ||
		!Number.isFinite(rect.y) ||
		!Number.isFinite(rect.width) ||
		!Number.isFinite(rect.height)
	) {
		return false;
	}
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
function resolveOverlaps(
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

// Settles `movable` into the tightest gap-free arrangement possible without
// overlapping `obstacles` (never themselves moved) or each other - sliding
// each widget up, then left, as far as it can go, and repeating that to a
// fixed point so a widget freed up by an earlier widget's own move gets a
// chance to slide further in a later pass (#830: closes gaps on BOTH axes
// automatically after every placement/removal, not just vertically - a
// widget shrunk or removed used to leave a horizontal hole next to it that
// nothing ever reflowed into, which read as the grid being only halfway
// "automatic"). Bounded to terminate: every successful move strictly
// decreases some widget's x+y, floored at 1 on both axes.
function compactAgainstObstacles(
	movable: PlacedWidget[],
	obstacles: PlacedWidget[],
): PlacedWidget[] {
	const settled = movable.map((w) => ({ ...w }));
	let changed = true;
	while (changed) {
		changed = false;
		// Re-sort every pass (top-to-bottom, then left-to-right) against the
		// CURRENT positions - a widget that slid up in an earlier pass may now
		// be the thing a later widget in this same pass should settle against.
		const order = settled
			.map((_, index) => index)
			.sort(
				(a, b) => settled[a].y - settled[b].y || settled[a].x - settled[b].x,
			);
		for (const i of order) {
			const widget = settled[i];
			const others = [...obstacles, ...settled.filter((_, j) => j !== i)];

			let bestY = widget.y;
			for (let y = widget.y - 1; y >= 1; y--) {
				const candidate = { ...widget, y };
				if (others.some((o) => rectsOverlap(candidate, o))) break;
				bestY = y;
			}

			let bestX = widget.x;
			for (let x = widget.x - 1; x >= 1; x--) {
				const candidate = { ...widget, x, y: bestY };
				if (others.some((o) => rectsOverlap(candidate, o))) break;
				bestX = x;
			}

			if (bestY !== widget.y || bestX !== widget.x) {
				settled[i] = { ...widget, x: bestX, y: bestY };
				changed = true;
			}
		}
	}
	return settled;
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

// A single horizontal run of the layout: a maximal group of widgets whose Y
// ranges are connected, transitively (#1932). OrgDashboardPage/index.tsx
// renders each band returned by groupIntoRowBands below as its own
// independent grid container, capped to `columns` wide instead of always
// spanning the full GRID_COLUMNS - so a band that never reaches the full
// width (most commonly a lightly-customized layout that trimmed some
// widgets down without ever widening them back out) doesn't leave the rest
// of that width sitting permanently blank next to it. `startRow` is the
// band's own topmost absolute row; a caller remaps each widget's stored
// (grid-absolute) y into a position relative to the band's own container
// via `y - startRow + 1`.
export interface LayoutRowBand {
	startRow: number;
	columns: number;
	widgets: PlacedWidget[];
}

// Splits `widgets` into row bands (see LayoutRowBand above). Two widgets
// belong to the same band whenever their Y ranges overlap, transitively - a
// row-by-row scan wouldn't work here: a single tall widget (e.g. a 4-row
// Calendar) shares its band with anything beside it in ANY of those 4 rows,
// not just the first one. Sorting by y and starting a new band only when a
// widget's own y falls strictly past the current band's running bottom edge
// is the standard interval-merging algorithm applied to just the Y axis,
// ignoring x/width entirely - correct regardless of input order, including
// ties (a widget sharing its y with the band's first widget always finds
// bottom already >= its own y, from that first widget's own height alone).
export function groupIntoRowBands(widgets: PlacedWidget[]): LayoutRowBand[] {
	const sorted = [...widgets].sort((a, b) => a.y - b.y);
	const bands: PlacedWidget[][] = [];
	let bottom = -Infinity;
	for (const widget of sorted) {
		if (bands.length === 0 || widget.y > bottom) {
			bands.push([]);
			bottom = -Infinity;
		}
		bands[bands.length - 1].push(widget);
		bottom = Math.max(bottom, widget.y + widget.height - 1);
	}

	return bands.map((bandWidgets) => ({
		startRow: Math.min(...bandWidgets.map((w) => w.y)),
		columns: Math.max(...bandWidgets.map((w) => w.x + w.width - 1)),
		// sortByPosition (not just the y-only sort above, and not the input's
		// own array order) so DOM/tab order within a band reads top-to-bottom,
		// then left-to-right, the same way mobile's stacked single-column
		// rendering already does - OrgDashboardPage/index.tsx renders each
		// band as its own real CSS grid, so every widget still gets its own
		// explicit gridColumn/gridRow and doesn't visually depend on this
		// order, but a screen-reader/keyboard user tabbing through a band
		// should still land on its widgets in reading order rather than
		// whatever order edit history happened to leave them in the layout
		// array (see sortByPosition's own doc comment for why that array
		// order can't be trusted directly).
		widgets: sortByPosition(bandWidgets),
	}));
}

// Places a newly added widget into the first available empty cell(s) on the
// grid, at its catalog default size (#1917), instead of always appending
// below every existing widget in column 1 - that ignored gaps elsewhere on
// the grid, so a partially-filled layout with open columns beside its
// existing widgets still had every new addition stack underneath column 1,
// one on top of the next. Scans row-major (top-to-bottom, then left-to-right
// within a row) for the first position the widget's footprint fits without
// overlapping anything already placed.
export function placeNewWidget(
	widgetKey: WidgetKey,
	existing: PlacedWidget[],
): PlacedWidget {
	const { defaultWidth: width, defaultHeight: height } =
		WIDGET_CATALOG[widgetKey];
	const bottom = existing.reduce((max, w) => Math.max(max, w.y + w.height), 1);
	for (let y = 1; y <= bottom; y++) {
		for (let x = 1; x + width - 1 <= GRID_COLUMNS; x++) {
			const candidate: PlacedWidget = { widgetKey, x, y, width, height };
			if (!existing.some((w) => rectsOverlap(candidate, w))) {
				return candidate;
			}
		}
	}
	// Unreachable: row `bottom` (by definition) has nothing occupying it or
	// any row below, so the y === bottom, x === 1 candidate above always
	// matches before the loop runs out - this return only exists to give
	// every code path a value for the type checker.
	return { widgetKey, x: 1, y: bottom, width, height };
}
