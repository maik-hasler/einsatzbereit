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

export function clearActiveOrgId(): void {
	document.cookie = `${COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;
}

export function resolveActiveOrg(
	orgs: OrganizationSummaryDto[],
	activeOrgId: string | null,
): OrganizationSummaryDto | null {
	if (orgs.length === 0) return null;
	if (orgs.length === 1) return orgs[0];

	const active = activeOrgId
		? orgs.find((org) => org.id === activeOrgId)
		: undefined;
	return active ?? [...orgs].sort((a, b) => a.name.localeCompare(b.name))[0];
}

export function resolveOrgAppPath(
	orgs: OrganizationSummaryDto[],
	activeOrgId: string | null,
): string | null {
	const org = resolveActiveOrg(orgs, activeOrgId);
	return org ? `/app/${org.id}/dashboard` : null;
}
