import type { OrganizationSummaryDto } from "../client/api-client";
import { orgTabPath } from "./orgTabs";

export type StaticNavLinkKey = "home" | "findOpportunities" | "organizations";

export type StaticNavLink = {
	kind: "static";
	key: StaticNavLinkKey;
	to: string;
};

export type OrganizationNavLink = {
	kind: "organization";
	key: "organization";
	to: string;
	org: OrganizationSummaryDto;
};

export type PrimaryNavLink = StaticNavLink | OrganizationNavLink;

const STATIC_LINKS: readonly StaticNavLink[] = [
	{ kind: "static", key: "home", to: "/" },
	{ kind: "static", key: "findOpportunities", to: "/opportunities" },
	{ kind: "static", key: "organizations", to: "/organizations" },
];

export function buildPrimaryNav(
	activeOrg?: OrganizationSummaryDto | null,
): PrimaryNavLink[] {
	if (!activeOrg) return [...STATIC_LINKS];

	return [
		...STATIC_LINKS,
		{
			kind: "organization",
			key: "organization",
			to: orgTabPath(activeOrg.id, "dashboard"),
			org: activeOrg,
		},
	];
}
