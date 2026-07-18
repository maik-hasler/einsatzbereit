import type { OrganizationSummaryDto } from "../client/api-client";

const COOKIE_NAME = "active-org";
const COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365;

export function getActiveOrgId(): string | null {
	const match = document.cookie.match(
		new RegExp(`(?:^|; )${COOKIE_NAME}=([^;]*)`),
	);
	return match ? decodeURIComponent(match[1]) : null;
}

export function setActiveOrgId(organizationId: string): void {
	document.cookie = `${COOKIE_NAME}=${encodeURIComponent(organizationId)}; path=/; max-age=${COOKIE_MAX_AGE_SECONDS}; SameSite=Lax`;
}

// Resolves where the org app shell should open without ever showing an
// intermediate picker: the last-opened org (active-org cookie) if the user
// still belongs to it, otherwise the first org alphabetically by name.
export function resolveOrgAppPath(
	orgs: OrganizationSummaryDto[],
	activeOrgId: string | null,
): string | null {
	if (orgs.length === 0) return null;
	if (orgs.length === 1) return `/app/${orgs[0].id}/dashboard`;

	const active = activeOrgId
		? orgs.find((org) => org.id === activeOrgId)
		: undefined;
	const target =
		active ?? [...orgs].sort((a, b) => a.name.localeCompare(b.name))[0];
	return `/app/${target.id}/dashboard`;
}
