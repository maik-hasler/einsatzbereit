import type { OrganizationSummaryDto } from "../client/api-client";
import { orgTabPath } from "./orgTabs";

export type StaticNavLinkKey =
	"home" | "findOpportunities" | "organizations" | "forOrganizations";

export type StaticNavLink = {
	kind: "static";
	key: StaticNavLinkKey;
	to: string;

	hash: boolean;
};

export type OrganizationNavLink = {
	kind: "organization";
	key: "organization";
	to: string;
	org: OrganizationSummaryDto;
};

export type PrimaryNavLink = StaticNavLink | OrganizationNavLink;

const STATIC_LINKS: readonly StaticNavLink[] = [
	{ kind: "static", key: "home", to: "/", hash: false },
	{
		kind: "static",
		key: "findOpportunities",
		to: "/opportunities",
		hash: false,
	},
	{
		kind: "static",
		key: "organizations",
		to: "/organizations",
		hash: false,
	},
	{
		kind: "static",
		key: "forOrganizations",
		to: "/#for-organizations",
		hash: true,
	},
];

export function buildPrimaryNav(
	activeOrg?: OrganizationSummaryDto | null,
): PrimaryNavLink[] {
	return STATIC_LINKS.map((link) =>
		link.key === "forOrganizations" && activeOrg
			? {
					kind: "organization",
					key: "organization",
					to: orgTabPath(activeOrg.id, "dashboard"),
					org: activeOrg,
				}
			: link,
	);
}
