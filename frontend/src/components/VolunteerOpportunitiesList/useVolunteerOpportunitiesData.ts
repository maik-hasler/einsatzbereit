import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { fetchVolunteerOpportunities } from "../../lib/volunteerOpportunities";

// Matches the results grid's own breakpoints (grid-cols-1 sm:grid-cols-2
// xl:grid-cols-3 in OpportunityResultsList.tsx) so a fully-loaded page is
// always a whole number of rows - the opportunities-fade-tail rule in
// global.css fades exactly the last row on whichever tier is active, which
// only lines up if the page size itself is a multiple of that tier's column
// count. 1280px/640px are hardcoded to match Tailwind's xl/sm defaults, the
// same approach OrgDashboardPage's useIsLargeViewport takes for its own
// breakpoint.
const XL_QUERY = "(min-width: 1280px)";
const SM_QUERY = "(min-width: 640px)";

function computePageSize(): number {
	if (window.matchMedia(XL_QUERY).matches) return 9; // 3 cols x 3 rows
	if (window.matchMedia(SM_QUERY).matches) return 8; // 2 cols x 4 rows
	return 5; // 1 col
}

function useOpportunitiesPageSize(): number {
	const [pageSize, setPageSize] = useState(computePageSize);
	useEffect(() => {
		const xlQuery = window.matchMedia(XL_QUERY);
		const smQuery = window.matchMedia(SM_QUERY);
		const handler = () => setPageSize(computePageSize());
		xlQuery.addEventListener("change", handler);
		smQuery.addEventListener("change", handler);
		return () => {
			xlQuery.removeEventListener("change", handler);
			smQuery.removeEventListener("change", handler);
		};
	}, []);
	return pageSize;
}

export interface VolunteerOpportunitiesFilters {
	occurrence: string;
	participationType: string;
	isRemoteParam: string;
	dateFrom: string;
	dateTo: string;
	categoriesParam: string;
	tag: string;
	keyword: string;
	lat: string;
	lng: string;
	radius: string;
}

export function useVolunteerOpportunitiesData(
	filters: VolunteerOpportunitiesFilters,
) {
	const api = useApiClient();
	const { t } = useTranslation();
	const pageSize = useOpportunitiesPageSize();

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

	const selectedCategories = categoriesParam
		? categoriesParam.split(",").filter(Boolean)
		: [];
	const hasLocation = !!(lat && lng && radius);

	return useLoadMore<VolunteerOpportunitySummary>(
		(pageNumber) => {
			const isRemoteBool =
				isRemoteParam === "true"
					? true
					: isRemoteParam === "false"
						? false
						: undefined;
			// Both ends pinned to the visitor's own day boundaries. A bare
			// `new Date("2026-08-15")` is UTC midnight, so east of Greenwich the
			// range used to start two hours into its first day and stop the instant
			// its last day began - dropping everything actually happening on the day
			// the visitor clicked last, and contradicting the availability marks the
			// calendar now draws (#1779). Same parse as MiniCalendar's own parseIso.
			const dateFromParsed = dateFrom
				? new Date(`${dateFrom}T00:00:00`)
				: undefined;
			const dateToParsed = dateTo
				? new Date(`${dateTo}T23:59:59.999`)
				: undefined;
			const centerLatitude = hasLocation ? parseFloat(lat) : undefined;
			const centerLongitude = hasLocation ? parseFloat(lng) : undefined;
			const radiusKm = hasLocation ? parseFloat(radius) : undefined;

			return fetchVolunteerOpportunities(api, {
				pageNumber,
				pageSize,
				occurrence: occurrence || undefined,
				participationType: participationType || undefined,
				isRemote: isRemoteBool,
				dateFrom: dateFromParsed,
				dateTo: dateToParsed,
				centerLatitude,
				centerLongitude,
				radiusKm,
				categories:
					selectedCategories.length > 0 ? selectedCategories : undefined,
				tag: tag || undefined,
				keyword: keyword || undefined,
			});
		},
		{
			deps: [
				pageSize,
				lat,
				lng,
				radius,
				occurrence,
				participationType,
				isRemoteParam,
				dateFrom,
				dateTo,
				categoriesParam,
				tag,
				keyword,
			],
			getErrorMessage: (err) => getApiErrorMessage(err, t("error.serverError")),
		},
	);
}
