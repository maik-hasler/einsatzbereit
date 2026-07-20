// Single source of truth for the org app shell's tabs, shared between the
// tab bar rendered inside OrgAppLayout and any org-scoped nav link that needs
// to jump straight to one of them (e.g. Header's mobile burger submenu).
export const ORG_TABS = [
	{ key: "dashboard", labelKey: "orgOverview.tabCalendar" },
	{ key: "opportunities", labelKey: "orgOverview.tabOpportunities" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
] as const;
