import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { fetchVolunteerOpportunities } from "../../lib/volunteerOpportunities";

const LIST_PAGE_SIZE = 10;

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
			const dateFromParsed = dateFrom ? new Date(dateFrom) : undefined;
			const dateToParsed = dateTo ? new Date(dateTo) : undefined;
			const centerLatitude = hasLocation ? parseFloat(lat) : undefined;
			const centerLongitude = hasLocation ? parseFloat(lng) : undefined;
			const radiusKm = hasLocation ? parseFloat(radius) : undefined;

			return fetchVolunteerOpportunities(api, {
				pageNumber,
				pageSize: LIST_PAGE_SIZE,
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
