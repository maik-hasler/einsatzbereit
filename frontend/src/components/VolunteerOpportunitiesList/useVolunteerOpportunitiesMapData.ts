import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import { fetchVolunteerOpportunities } from "../../lib/volunteerOpportunities";
import type { VolunteerOpportunitiesFilters } from "./useVolunteerOpportunitiesData";

// GetVolunteerOpportunitiesQueryHandler clamps PageSize to this server-side -
// the map view fetches one page at that cap instead of the list's small
// per-viewport page size (see useOpportunitiesPageSize in
// useVolunteerOpportunitiesData.ts), since the point of a map is to see every
// matching pin at a glance rather than paging through them with "Load more".
export const MAP_PAGE_SIZE = 100;

/**
 * Data for the browse page's map view (#1851): every on-site, geocoded
 * opportunity matching the current filter, fetched in a single page instead
 * of the list's incremental "load more" pagination. Only fetches while
 * `enabled` is true, so switching between list and map view doesn't run both
 * requests at once.
 *
 * Unlike useVolunteerOpportunitiesData, a failed fetch has no automatic
 * retry-on-reconnect - the map is a secondary view a volunteer opts into, and
 * the retry button LoadMoreError already renders covers the same recovery
 * without the extra state.
 */
export function useVolunteerOpportunitiesMapData(
	filters: VolunteerOpportunitiesFilters,
	enabled: boolean,
) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [pins, setPins] = useState<VolunteerOpportunitySummary[]>([]);
	const [loading, setLoading] = useState(enabled);
	const [error, setError] = useState<string | null>(null);
	const [truncated, setTruncated] = useState(false);
	const [retryToken, setRetryToken] = useState(0);

	// Switching list -> map sets `enabled` true a render before the effect
	// below gets to run `setLoading(true)` - without this, that one render
	// would paint OpportunityResultsMap with the still-false `loading` from
	// the last time the map was open, flashing its empty/stale state for a
	// frame. Adjusting state during render (not in an effect) is the
	// documented fix for "state needs to change together with a prop" -
	// https://react.dev/learn/you-might-not-need-an-effect - React reruns this
	// render immediately with the corrected state instead of committing the
	// stale one.
	const [prevEnabled, setPrevEnabled] = useState(enabled);
	if (enabled !== prevEnabled) {
		setPrevEnabled(enabled);
		if (enabled) setLoading(true);
	}

	const {
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		categoriesParam,
		tag,
		keyword,
		lat,
		lng,
		radius,
	} = filters;

	useEffect(() => {
		if (!enabled) return;

		const selectedCategories = categoriesParam
			? categoriesParam.split(",").filter(Boolean)
			: [];
		const hasLocation = !!(lat && lng && radius);
		// Same local-day parse as useVolunteerOpportunitiesData/MiniCalendar - see
		// einsatzbereit#1779.
		const dateFromParsed = dateFrom
			? new Date(`${dateFrom}T00:00:00`)
			: undefined;
		const dateToParsed = dateTo
			? new Date(`${dateTo}T23:59:59.999`)
			: undefined;

		const controller = new AbortController();
		setLoading(true);
		setError(null);

		fetchVolunteerOpportunities(
			api,
			{
				pageNumber: 1,
				pageSize: MAP_PAGE_SIZE,
				occurrence: occurrence || undefined,
				participationType: participationType || undefined,
				isRemote:
					isRemoteParam === "true"
						? true
						: isRemoteParam === "false"
							? false
							: undefined,
				dateFrom: dateFromParsed,
				dateTo: dateToParsed,
				centerLatitude: hasLocation ? parseFloat(lat) : undefined,
				centerLongitude: hasLocation ? parseFloat(lng) : undefined,
				radiusKm: hasLocation ? parseFloat(radius) : undefined,
				categories:
					selectedCategories.length > 0 ? selectedCategories : undefined,
				tag: tag || undefined,
				keyword: keyword || undefined,
			},
			controller.signal,
		)
			.then((page) => {
				if (controller.signal.aborted) return;
				setPins(
					page.items.filter(
						(item) =>
							!item.isRemote && item.latitude != null && item.longitude != null,
					),
				);
				setTruncated((page.pageCount ?? 1) > 1);
			})
			.catch((err) => {
				if (controller.signal.aborted) return;
				setError(getApiErrorMessage(err, t("error.serverError")));
			})
			.finally(() => {
				if (!controller.signal.aborted) setLoading(false);
			});

		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [
		enabled,
		retryToken,
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		categoriesParam,
		tag,
		keyword,
		lat,
		lng,
		radius,
	]);

	const retry = useCallback(() => setRetryToken((n) => n + 1), []);

	return { pins, loading, error, truncated, retry };
}
