export interface OrgTab {
	key: string;
	labelKey: string;

	organizerOnly?: boolean;
}

export const ORG_TABS: readonly OrgTab[] = [
	{ key: "dashboard", labelKey: "orgOverview.tabDashboard" },
	{ key: "opportunities", labelKey: "orgOverview.tabOpportunities" },
	// The sign-ups listing is the one section of the org app a plain member
	// cannot use at all: GET /v1/organizations/{id}/engagements answers them
	// with 403 Organization.NotOrganizer, so offering the tab would only route
	// them into a request that is guaranteed to fail (#2316). Every other tab
	// backs onto a member-readable endpoint and gates its organizer-only
	// controls individually.
	{
		key: "engagements",
		labelKey: "orgOverview.tabEngagements",
		organizerOnly: true,
	},
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
];

export function visibleOrgTabs(isOrganizer: boolean): readonly OrgTab[] {
	return isOrganizer ? ORG_TABS : ORG_TABS.filter((tab) => !tab.organizerOnly);
}

export function canViewOrgTab(tabKey: string, isOrganizer: boolean): boolean {
	return visibleOrgTabs(isOrganizer).some((tab) => tab.key === tabKey);
}

export function orgTabPath(organizationId: string, tabKey: string): string {
	return tabKey === "dashboard"
		? `/app/${organizationId}/dashboard`
		: `/app/${organizationId}/dashboard/${tabKey}`;
}
