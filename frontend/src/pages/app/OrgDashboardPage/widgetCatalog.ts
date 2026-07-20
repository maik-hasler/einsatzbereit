// Widget keys/sizes mirror the backend's DashboardWidgetKey/DashboardWidgetSize
// enums exactly (see backend/src/Domain/Organizations/DashboardWidget*.cs) -
// they travel to/from the API as these same string literals.
export type WidgetKey =
	| "ToDo"
	| "UpcomingOpportunities"
	| "Calendar"
	| "Settings"
	| "CreateOpportunity"
	| "QuickCheckIn"
	| "SettingsIcon";

export type WidgetSize = "Small" | "Medium" | "Large";

export interface WidgetPlacement {
	widgetKey: WidgetKey;
	size: WidgetSize;
}

interface WidgetCatalogEntry {
	titleKey: string;
	// Sizes an organizer may pick for this widget in edit mode. A single
	// entry means the size can't be changed (no size-cycle control shown).
	allowedSizes: WidgetSize[];
	defaultSize: WidgetSize;
}

export const WIDGET_CATALOG: Record<WidgetKey, WidgetCatalogEntry> = {
	ToDo: {
		titleKey: "orgDashboard.todoWidgetTitle",
		allowedSizes: ["Medium", "Large"],
		defaultSize: "Medium",
	},
	UpcomingOpportunities: {
		titleKey: "orgDashboard.upcomingWidgetTitle",
		allowedSizes: ["Medium", "Large"],
		defaultSize: "Medium",
	},
	Calendar: {
		titleKey: "orgDashboard.calendarWidgetTitle",
		// Large by default (matches the pre-existing layout), but an organizer
		// who doesn't want a full-width calendar can size it down to sit
		// beside another widget instead (#771 follow-up review feedback -
		// "I cant put a calendar beside another component").
		allowedSizes: ["Medium", "Large"],
		defaultSize: "Large",
	},
	Settings: {
		titleKey: "orgDashboard.settingsWidgetTitle",
		allowedSizes: ["Medium", "Large"],
		defaultSize: "Large",
	},
	CreateOpportunity: {
		titleKey: "orgDashboard.createOpportunityWidgetTitle",
		allowedSizes: ["Small", "Medium"],
		defaultSize: "Small",
	},
	QuickCheckIn: {
		titleKey: "orgDashboard.quickCheckInWidgetTitle",
		allowedSizes: ["Small", "Medium"],
		defaultSize: "Small",
	},
	SettingsIcon: {
		titleKey: "orgDashboard.settingsIconWidgetTitle",
		allowedSizes: ["Small"],
		defaultSize: "Small",
	},
};

export const WIDGET_KEYS = Object.keys(WIDGET_CATALOG) as WidgetKey[];

// Layout applied when an organizer hasn't customized their dashboard yet -
// matches the previous hardcoded dashboard (ToDo+Upcoming side by side,
// Calendar and Settings full-width below), plus the former standalone
// "+ Create Opportunity" button folded into its own widget tile.
export const DEFAULT_LAYOUT: WidgetPlacement[] = [
	{ widgetKey: "CreateOpportunity", size: "Small" },
	{ widgetKey: "ToDo", size: "Medium" },
	{ widgetKey: "UpcomingOpportunities", size: "Medium" },
	{ widgetKey: "Calendar", size: "Large" },
	{ widgetKey: "Settings", size: "Large" },
];

const SIZE_COL_SPAN: Record<WidgetSize, string> = {
	Small: "lg:col-span-1",
	Medium: "lg:col-span-2",
	Large: "lg:col-span-4",
};

export function widgetColSpanClass(size: WidgetSize): string {
	return SIZE_COL_SPAN[size];
}

// Defends against a saved layout referencing a widget key/size this build no
// longer knows about (e.g. a widget retired in a later release) - drops
// widgets with an unrecognized key entirely and falls back a stale size to
// that widget's default, rather than trusting the API response's strings
// as-is.
export function sanitizePlacement(w: {
	widgetKey: string;
	size: string;
}): WidgetPlacement | null {
	const entry = WIDGET_CATALOG[w.widgetKey as WidgetKey];
	if (!entry) return null;
	const size = entry.allowedSizes.includes(w.size as WidgetSize)
		? (w.size as WidgetSize)
		: entry.defaultSize;
	return { widgetKey: w.widgetKey as WidgetKey, size };
}
