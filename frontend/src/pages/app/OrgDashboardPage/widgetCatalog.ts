export type WidgetKey =
	| "ToDo"
	| "VolunteerStats"
	| "UpcomingOpportunities"
	| "Calendar"
	| "Settings"
	| "CreateOpportunity"
	| "QuickCheckIn";

// How wide the tile is, in bands rather than columns, so a widget lays itself
// out against the room it actually has instead of against a column count.
export type WidgetWidthClass = "compact" | "medium" | "full";

// How tall it is. A one-row tile is a strip - roughly a button's worth of
// height once the card's padding and label are paid for - and nothing that
// wants a list or a chart fits in one. Widgets used to be told only how WIDE
// they were, so a full-width strip and a full-width canvas were handed the
// same "full" and rendered the same crushed layout.
export type WidgetHeightClass = "strip" | "short" | "tall";

export interface WidgetSize {
	width: WidgetWidthClass;
	height: WidgetHeightClass;
}

interface WidgetCatalogEntry {
	titleKey: string;

	defaultWidth: number;
	defaultHeight: number;

	minWidth: number;
	minHeight: number;
}

export const WIDGET_CATALOG: Record<WidgetKey, WidgetCatalogEntry> = {
	// The triage queue, and the reason an organizer opens the dashboard at all,
	// so it gets the top-left cell and enough height to list real rows.
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		defaultWidth: 3,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 1,
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		defaultWidth: 3,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
	// Not one button in a card any more - the shortcuts an organizer reaches for
	// between the two lists beside it.
	CreateOpportunity: {
		titleKey: "orgDashboard.quickActionsWidgetTitle",
		defaultWidth: 2,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 1,
	},
	VolunteerStats: {
		titleKey: "orgDashboard.volunteerStatsWidgetTitle",
		defaultWidth: 2,
		defaultHeight: 1,
		minWidth: 2,
		minHeight: 1,
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		defaultWidth: 8,
		defaultHeight: 4,
		minWidth: 4,
		minHeight: 4,
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		defaultWidth: 3,
		defaultHeight: 1,
		minWidth: 2,
		minHeight: 1,
	},
	QuickCheckIn: {
		titleKey: "orgDashboard.quickCheckInWidgetTitle",
		defaultWidth: 3,
		defaultHeight: 2,
		minWidth: 2,
		minHeight: 2,
	},
};

export const WIDGET_KEYS = Object.keys(WIDGET_CATALOG) as WidgetKey[];

export const GRID_COLUMNS = 8;

export const GRID_MAX_ROWS = 100;

export interface PlacedWidget {
	widgetKey: WidgetKey;
	x: number;
	y: number;
	width: number;
	height: number;
}

// What a dashboard nobody has customized is for: the two questions an
// organizer opens it with - what needs a decision from me, and what is
// happening next - side by side above the fold, the shortcuts they reach for
// beside them, and the calendar below as the thing you scroll to rather than
// the thing you land on. Everything else in the catalog is opt-in.
export const DEFAULT_LAYOUT: PlacedWidget[] = [
	{ widgetKey: "ToDo", x: 1, y: 1, width: 3, height: 2 },
	{ widgetKey: "UpcomingOpportunities", x: 4, y: 1, width: 3, height: 2 },
	{ widgetKey: "CreateOpportunity", x: 7, y: 1, width: 2, height: 2 },
	{ widgetKey: "Calendar", x: 1, y: 3, width: 8, height: 4 },
];

export function sortByPosition(widgets: PlacedWidget[]): PlacedWidget[] {
	return [...widgets].sort((a, b) => a.y - b.y || a.x - b.x);
}

export function classifyWidgetSize(width: number, height: number): WidgetSize {
	return {
		width: width <= 3 ? "compact" : width <= 5 ? "medium" : "full",
		height: height <= 1 ? "strip" : height <= 2 ? "short" : "tall",
	};
}

// One column, content-sized rows: a phone gives a widget the full width of a
// narrow screen and as much height as it asks for, which is neither of the
// desktop extremes.
export const MOBILE_WIDGET_SIZE: WidgetSize = {
	width: "compact",
	height: "short",
};

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

export function isValidPlacement(rect: PlacedWidget): boolean {
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

// Distance from a widget's current cell to a candidate one, used to pick
// where a displaced widget lands. A row apart costs a full board width, so
// "stay in this row band if anything there still fits" always beats "slide
// sideways into the next band" - a widget nudged out of row 1 should settle
// beside its neighbours, not below them.
function placementDistance(from: PlacedWidget, toX: number, toY: number) {
	return Math.abs(toY - from.y) * GRID_COLUMNS + Math.abs(toX - from.x);
}

// The free cell nearest to where `widget` sits now, never below its current
// row - moving it down is what reorders the board, and is left to the push
// below. Returns null when its own row band and everything above it is full.
function nearestFreeSpotAtOrAbove(
	widget: PlacedWidget,
	obstacles: PlacedWidget[],
): PlacedWidget | null {
	let best: PlacedWidget | null = null;
	let bestDistance = Infinity;
	for (let y = 1; y <= widget.y; y++) {
		for (let x = 1; x + widget.width - 1 <= GRID_COLUMNS; x++) {
			const candidate = { ...widget, x, y };
			if (obstacles.some((o) => rectsOverlap(candidate, o))) continue;
			const distance = placementDistance(widget, x, y);
			if (distance < bestDistance) {
				bestDistance = distance;
				best = candidate;
			}
		}
	}
	return best;
}

// Resolves whatever overlaps are left by pushing widgets straight down, each
// by the least it takes to clear what is already settled above it. Processing
// top-to-bottom means a widget is only ever pushed past widgets that started
// above it, so the board keeps its reading order instead of shuffling.
function pushDownCollisions(
	widgets: PlacedWidget[],
	fixed: PlacedWidget[],
): PlacedWidget[] {
	const result = widgets.map((widget) => ({ ...widget }));
	const order = result
		.map((_, index) => index)
		.sort((a, b) => result[a].y - result[b].y || result[a].x - result[b].x);

	const settled = [...fixed];
	for (const index of order) {
		let widget = result[index];
		let pushed = true;
		while (pushed) {
			pushed = false;
			for (const other of settled) {
				if (rectsOverlap(widget, other) && other.y + other.height > widget.y) {
					widget = { ...widget, y: other.y + other.height };
					pushed = true;
				}
			}
		}
		result[index] = widget;
		settled.push(widget);
	}
	return result;
}

// Clears `rect`'s footprint by relocating only what it actually lands on.
//
// This used to push every overlapping widget straight down to just below
// whatever it collided with, and repeat until nothing overlapped. Each push
// landed the widget on the NEXT widget down, so a single sideways nudge in
// row 1 cascaded through the whole board and exiled its neighbour to the very
// bottom, past the calendar and everything else - and compaction could not
// undo it, because compaction only pulls up and left, and the column the
// widget started in no longer had a free path back up (#2322 F1).
//
// A displaced widget now looks for the nearest free space in its own row band
// first, so nudging a card sideways moves its neighbour sideways too. Only a
// widget with genuinely nowhere left to sit up there (a full-width tile, say)
// drops below the placement - and then the push is minimal and ordered, so
// what was under it stays under it.
function relocateDisplaced(
	rect: PlacedWidget,
	others: PlacedWidget[],
): PlacedWidget[] {
	const result = others.map((widget) => ({ ...widget }));

	const displaced = result
		.map((_, index) => index)
		.filter((index) => rectsOverlap(result[index], rect))
		.sort((a, b) => result[a].y - result[b].y || result[a].x - result[b].x);

	// A widget still waiting to be moved is vacating its cells, so it must
	// not block the one being placed right now.
	const pending = new Set(displaced);
	for (const index of displaced) {
		pending.delete(index);
		const obstacles = [
			rect,
			...result.filter((_, j) => j !== index && !pending.has(j)),
		];
		result[index] = nearestFreeSpotAtOrAbove(result[index], obstacles) ??
			// Nowhere beside or above it: start just under the placement and
			// let the push below settle it against whatever is already there.
			{ ...result[index], y: rect.y + rect.height };
	}

	return pushDownCollisions(result, [rect]);
}

function compactAgainstObstacles(
	movable: PlacedWidget[],
	obstacles: PlacedWidget[],
): PlacedWidget[] {
	const settled = movable.map((w) => ({ ...w }));
	let changed = true;
	while (changed) {
		changed = false;

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

export function compactLayout(widgets: PlacedWidget[]): PlacedWidget[] {
	return compactAgainstObstacles(widgets, []);
}

export function settlePlacement(
	rect: PlacedWidget,
	others: PlacedWidget[],
): PlacedWidget[] {
	const relocated = relocateDisplaced(rect, others);
	return [rect, ...compactAgainstObstacles(relocated, [rect])];
}

export interface LayoutRowBand {
	startRow: number;
	columns: number;
	widgets: PlacedWidget[];
}

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

		widgets: sortByPosition(bandWidgets),
	}));
}

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

	return { widgetKey, x: 1, y: bottom, width, height };
}
