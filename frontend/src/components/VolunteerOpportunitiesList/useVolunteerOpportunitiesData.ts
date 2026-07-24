import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
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
		lat,
		lng,
		radius,
	} = filters;

	const selectedCategories = categoriesParam
		? categoriesParam.split(",").filter(Boolean)
		: [];
	const hasLocation = !!(lat && lng && radius);

	const [items, setItems] = useState<VolunteerOpportunitySummary[]>([]);
	const [page, setPage] = useState(1);
	const [pageCount, setPageCount] = useState(1);
	const [loading, setLoading] = useState(true);
	const [loadingMore, setLoadingMore] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const prevFiltersRef = useRef({
		lat,
		lng,
		radius,
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		categories: categoriesParam,
		tag,
	});

	useEffect(() => {
		const prev = prevFiltersRef.current;
		const filterChanged =
			prev.lat !== lat ||
			prev.lng !== lng ||
			prev.radius !== radius ||
			prev.occurrence !== occurrence ||
			prev.participationType !== participationType ||
			prev.isRemoteParam !== isRemoteParam ||
			prev.dateFrom !== dateFrom ||
			prev.dateTo !== dateTo ||
			prev.categories !== categoriesParam ||
			prev.tag !== tag;

		prevFiltersRef.current = {
			lat,
			lng,
			radius,
			occurrence,
			participationType,
			isRemoteParam,
			dateFrom,
			dateTo,
			categories: categoriesParam,
			tag,
		};

		if (filterChanged) {
			setItems([]);
			if (page !== 1) {
				setPage(1);
				return;
			}
		}

		if (page > 1) setLoadingMore(true);
		else setLoading(true);
		setError(null);

		let cancelled = false;
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

		fetchVolunteerOpportunities(api, {
			pageNumber: page,
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
		})
			.then((result) => {
				if (cancelled) return;
				if (page === 1) setItems(result.items);
				else setItems((prevItems) => [...prevItems, ...result.items]);
				setPageCount(result.pageCount ?? 1);
				setLoading(false);
				setLoadingMore(false);
			})
			.catch((err) => {
				if (cancelled) return;
				setError(getApiErrorMessage(err, t("error.serverError")));
				setLoading(false);
				setLoadingMore(false);
			});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [
		page,
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
	]);

	return { items, page, setPage, pageCount, loading, loadingMore, error };
}
