export const ORG_TABS = [
	{ key: "dashboard", labelKey: "orgOverview.tabDashboard" },
	{ key: "opportunities", labelKey: "orgOverview.tabOpportunities" },
	{ key: "engagements", labelKey: "orgOverview.tabEngagements" },
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
] as const;

export function orgTabPath(organizationId: string, tabKey: string): string {
	return tabKey === "dashboard"
		? `/app/${organizationId}/dashboard`
		: `/app/${organizationId}/dashboard/${tabKey}`;
}
