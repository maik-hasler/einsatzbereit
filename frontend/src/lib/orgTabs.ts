// Single source of truth for the org app shell's tabs, shared between the
// tab bar rendered inside OrgAppLayout and any org-scoped nav link that needs
// to jump straight to one of them (e.g. Header's mobile burger submenu).
export const ORG_TABS = [
	{ key: "dashboard", labelKey: "orgOverview.tabDashboard" },
	{ key: "opportunities", labelKey: "orgOverview.tabOpportunities" },
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
] as const;

// #9: opportunities/members/settings are nested under /dashboard/... in the
// URL (App.tsx's pathless "dashboard" parent route) while dashboard itself
// keeps its own bare path - every caller that builds a link to a tab (the
// tab bar, breadcrumb, and OrganizationSwitcher) goes through this instead
// of duplicating that nesting rule. Lives here rather than on OrgAppLayout
// itself so Header's burger submenu can use it too without an import cycle
// (Header renders inside OrgAppLayout).
export function orgTabPath(organizationId: string, tabKey: string): string {
	return tabKey === "dashboard"
		? `/app/${organizationId}/dashboard`
		: `/app/${organizationId}/dashboard/${tabKey}`;
}
