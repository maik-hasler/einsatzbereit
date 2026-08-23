import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { fetchVolunteerOpportunities } from "../../lib/volunteerOpportunities";

const XL_QUERY = "(min-width: 1280px)";
const SM_QUERY = "(min-width: 640px)";

function computePageSize(): number {
	if (window.matchMedia(XL_QUERY).matches) return 9;
	if (window.matchMedia(SM_QUERY).matches) return 8;
	return 5;
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
