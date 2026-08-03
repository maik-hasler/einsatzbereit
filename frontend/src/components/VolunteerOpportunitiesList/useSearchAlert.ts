import { useCallback, useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useApiClient } from "../../hooks/useApiClient";
import type { SearchAlertResponse } from "../../client/api-client";

export interface SearchAlertCriteria {
	occurrence?: string;
	participationType?: string;
	isRemote?: boolean;
	centerLatitude?: number;
	centerLongitude?: number;
	radiusKm?: number;
	categories?: string[];
	tag?: string;
}

// Anonymous visitors never have an alert to show - GetSearchAlert requires
// login (einsatzbereit#1090), so this never even calls the API for them.
export function useSearchAlert() {
	const auth = useAuth();
	const api = useApiClient();
	const [alert, setAlert] = useState<SearchAlertResponse | null>(null);
	const [saving, setSaving] = useState(false);

	const refresh = useCallback(async () => {
		if (!auth.isAuthenticated) {
			setAlert(null);
			return;
		}
		const result = await api.getSearchAlert();
		setAlert(result);
	}, [api, auth.isAuthenticated]);

	useEffect(() => {
		refresh();
	}, [refresh]);

	const save = useCallback(
		async (criteria: SearchAlertCriteria) => {
			setSaving(true);
			try {
				await api.saveSearchAlert({
					occurrence: criteria.occurrence,
					participationType: criteria.participationType,
					isRemote: criteria.isRemote,
					centerLatitude: criteria.centerLatitude,
					centerLongitude: criteria.centerLongitude,
					radiusKm: criteria.radiusKm,
					categories: criteria.categories,
					tag: criteria.tag,
				});
				await refresh();
			} finally {
				setSaving(false);
			}
		},
		[api, refresh],
	);

	const remove = useCallback(async () => {
		setSaving(true);
		try {
			await api.deleteSearchAlert();
			await refresh();
		} finally {
			setSaving(false);
		}
	}, [api, refresh]);

	return {
		hasActiveAlert: auth.isAuthenticated && (alert?.hasActiveAlert ?? false),
		isAuthenticated: auth.isAuthenticated,
		saving,
		save,
		remove,
	};
}
