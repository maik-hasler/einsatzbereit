export type WidgetKey =
	| "ToDo"
	| "VolunteerStats"
	| "UpcomingOpportunities"
	| "Calendar"
	| "Settings"
	| "CreateOpportunity"
	| "QuickCheckIn"
	| "SettingsIcon";

export type WidgetSizeClass = "compact" | "medium" | "full";

interface WidgetCatalogEntry {
	titleKey: string;

	defaultWidth: number;
	defaultHeight: number;

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

export const GRID_COLUMNS = 8;

export const GRID_MAX_ROWS = 100;

export interface PlacedWidget {
	widgetKey: WidgetKey;
	x: number;
	y: number;
	width: number;
	height: number;
}

export const DEFAULT_LAYOUT: PlacedWidget[] = [
	{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 3, height: 1 },
	{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
	{ widgetKey: "VolunteerStats", x: 7, y: 1, width: 2, height: 1 },
	{ widgetKey: "UpcomingOpportunities", x: 1, y: 2, width: 8, height: 2 },
	{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
	{ widgetKey: "Settings", x: 1, y: 8, width: 8, height: 1 },
];

export function sortByPosition(widgets: PlacedWidget[]): PlacedWidget[] {
	return [...widgets].sort((a, b) => a.y - b.y || a.x - b.x);
}

export function classifyWidth(width: number): WidgetSizeClass {
	if (width <= 3) return "compact";
	if (width <= 5) return "medium";
	return "full";
}

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
	const pushed = resolveOverlaps(rect, others);
	return [rect, ...compactAgainstObstacles(pushed, [rect])];
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
