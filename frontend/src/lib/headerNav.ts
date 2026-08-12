import type { OrganizationSummaryDto } from "../client/api-client";
import { orgTabPath } from "./orgTabs";

// The header's primary destinations, shared by DesktopHeader's top-level nav
// and MobileMenu's burger panel. The two used to keep hand-synced copies of
// the same array ("Kept in sync with DesktopHeader's own LINKS - the two are
// the same primary navigation at different breakpoints"), which is exactly the
// drift #1785 makes expensive: the organization entry below has to appear on
// both surfaces or it is missing at whichever width the reader happens to be.

export type StaticNavLinkKey =
	"home" | "findOpportunities" | "forOrganizations" | "help";

export type StaticNavLink = {
	kind: "static";
	key: StaticNavLinkKey;
	to: string;
	// Hash destinations are rendered as plain <a>, not router links - see
	// DesktopHeader's render branch for why.
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
		key: "forOrganizations",
		to: "/#for-organizations",
		hash: true,
	},
	{ kind: "static", key: "help", to: "/help", hash: false },
];

/**
 * The primary destinations for a given viewer.
 *
 * #1785: a member's own organization is a primary destination - the org app is
 * an organizer's main workspace, and it used to be reachable only by opening
 * the account menu and then a second disclosure inside it. It takes over the
 * "for organizations" slot rather than adding a fifth entry, for two reasons:
 *
 * - That slot is a hash link into the landing page's pitch *for people without
 *   an organization*. For a member it is the least useful of the four, and the
 *   organization they already belong to is what "organizations" means to them.
 * - The desktop nav has no room for a fifth label. Measured on live staging at
 *   768px in German, the four existing labels already need 627px of the 562px
 *   the row can give them (#1793 - they wrap); at 1024px only ~163px is free,
 *   less than an untruncated organization name. Swapping the slot keeps the
 *   entry count, and hence the wrap pressure, where it is today.
 *
 * Gated on membership rather than on the `organisator` role: `activeOrg` is
 * already resolved from the viewer's organization list (see resolveActiveOrg),
 * so a viewer with no membership keeps exactly today's four entries.
 */
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
