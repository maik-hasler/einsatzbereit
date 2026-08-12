import { useAuth } from "react-oidc-context";
import type { OrganizationSummaryDto } from "../client/api-client";
import { getActiveOrgId, resolveActiveOrg } from "../lib/activeOrg";
import { useApiClient } from "./useApiClient";
import { useSharedOrgFetch } from "./useSharedOrgFetch";

// The organizations the signed-in user belongs to, plus the one an org-scoped
// link should open (resolveActiveOrg). Header and HomePage each derived this
// from useSharedOrgFetch by hand; #1783 needed the same signal on a third
// surface (the profile settings page, to hide organizer-only email
// preferences from a volunteer who belongs to no organization), which would
// have been a third copy of the same `isLoggedIn ? (data ?? []) : []` plus
// loading/failed triad - so it lives here once instead.
//
// Only the derivation is shared: the underlying request is still deduplicated
// across all callers on the same mount by useSharedOrgFetch's in-flight
// registry (#1396), so any number of components may call this and the browser
// still issues a single GET /v1/organizations.
export function useMyOrganizations(): {
	orgs: OrganizationSummaryDto[];
	activeOrg: OrganizationSummaryDto | null;
	loading: boolean;
	failed: boolean;
	error: string | null;
} {
	const auth = useAuth();
	const api = useApiClient();
	const isLoggedIn = auth.isAuthenticated;

	const [orgsData, , error] = useSharedOrgFetch<OrganizationSummaryDto[]>(
		`organizations:${isLoggedIn}`,
		() => (isLoggedIn ? api.getOrganizations() : Promise.resolve([])),
	);

	const orgs = isLoggedIn ? (orgsData ?? []) : [];

	return {
		orgs,
		activeOrg: resolveActiveOrg(orgs, getActiveOrgId()),
		// useSharedOrgFetch leaves orgsData null both while the fetch is still
		// in flight and after it rejects. Callers that gate something on "this
		// user has no organizations" must be able to tell those apart - see
		// HomePage's create-org CTA (HomePageOrgCtaTests.cs) - so both are
		// surfaced separately rather than collapsed into an empty list.
		loading: isLoggedIn && orgsData === null && !error,
		failed: isLoggedIn && orgsData === null && !!error,
		error,
	};
}
