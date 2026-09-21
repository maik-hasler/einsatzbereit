import { useCallback } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "react-oidc-context";
import type { OrganizationSummaryDto } from "../client";
import { getActiveOrgId, resolveActiveOrg } from "../lib/activeOrg";
import { getApiErrorMessage } from "../lib/apiError";
import i18n from "../i18n";
import { queryKeys } from "../lib/queryKeys";
import { useApiClient } from "./useApiClient";

/**
 * Invalidates the caller's organization list.
 *
 * Every mutation that adds an organization, removes one, or changes the name
 * or logo the switcher renders has to call this. Before the list was cached it
 * was refetched on each mount, so a stale entry could not outlive a
 * navigation; now it can, and an organization created from the home page was
 * shown under the *previously* active organization's name for a full
 * `staleTime` because `resolveActiveOrg` could not find the new one in the
 * cached list.
 */
export function useInvalidateMyOrganizations(): () => Promise<void> {
	const queryClient = useQueryClient();
	return useCallback(
		() =>
			queryClient.invalidateQueries({
				queryKey: queryKeys.organizations.all,
			}),
		[queryClient],
	);
}

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

	const { data, error: queryError } = useQuery({
		queryKey: queryKeys.organizations.mine(),
		queryFn: () => api.getOrganizations({}),
		enabled: isLoggedIn,
	});

	const error = queryError
		? getApiErrorMessage(queryError, i18n.t("error.serverError"))
		: null;

	const orgs = isLoggedIn ? (data ?? []) : [];
	const pending = isLoggedIn && data === undefined;

	return {
		orgs,
		activeOrg: resolveActiveOrg(orgs, getActiveOrgId()),

		loading: pending && !error,
		failed: pending && !!error,
		error,
	};
}
