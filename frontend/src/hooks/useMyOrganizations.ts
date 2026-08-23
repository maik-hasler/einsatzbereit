import { useAuth } from "react-oidc-context";
import type { OrganizationSummaryDto } from "../client/api-client";
import { getActiveOrgId, resolveActiveOrg } from "../lib/activeOrg";
import { useApiClient } from "./useApiClient";
import { useSharedOrgFetch } from "./useSharedOrgFetch";

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

		loading: isLoggedIn && orgsData === null && !error,
		failed: isLoggedIn && orgsData === null && !!error,
		error,
	};
}
