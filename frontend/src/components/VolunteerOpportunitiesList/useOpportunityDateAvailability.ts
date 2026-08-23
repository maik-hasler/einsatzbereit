import { useEffect, useState } from "react";
import { useApiClient } from "../../hooks/useApiClient";
import { fetchVolunteerOpportunityDateAvailability } from "../../lib/volunteerOpportunities";
import type { VolunteerOpportunitiesFilters } from "./useVolunteerOpportunitiesData";

export interface VisibleMonth {
	year: number;

	month: number;
}

export type OpportunityDateAvailabilityFilters = Omit<
	VolunteerOpportunitiesFilters,
	"dateFrom" | "dateTo"
>;

const NO_AVAILABILITY: ReadonlyMap<string, number> = new Map();

export function useOpportunityDateAvailability(
	visibleMonth: VisibleMonth | null,
	filters: OpportunityDateAvailabilityFilters,
) {
	const api = useApiClient();
	const [availability, setAvailability] =
		useState<ReadonlyMap<string, number>>(NO_AVAILABILITY);
	const [loading, setLoading] = useState(false);

	const {
		occurrence,
		participationType,
		isRemoteParam,
		categoriesParam,
		tag,
		keyword,
		lat,
		lng,
		radius,
	} = filters;

	const year = visibleMonth?.year ?? null;
	const month = visibleMonth?.month ?? null;

	useEffect(() => {
		if (year === null || month === null) {
			setAvailability(NO_AVAILABILITY);
			setLoading(false);
			return;
		}

		const from = new Date(year, month, 1, 0, 0, 0, 0);
		const to = new Date(year, month + 1, 0, 23, 59, 59, 999);

		const selectedCategories = categoriesParam
			? categoriesParam.split(",").filter(Boolean)
			: [];
		const hasLocation = !!(lat && lng && radius);

		const controller = new AbortController();
		setLoading(true);

		fetchVolunteerOpportunityDateAvailability(
			api,
			{
				from,
				to,

				utcOffsetMinutes: -from.getTimezoneOffset(),
				occurrence: occurrence || undefined,
				participationType: participationType || undefined,
				isRemote:
					isRemoteParam === "true"
						? true
						: isRemoteParam === "false"
							? false
							: undefined,
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
			.then((days) => {
				setAvailability(
					new Map(days.map((day) => [day.date, day.opportunityCount])),
				);
			})
			.catch(() => {
				if (controller.signal.aborted) return;
				setAvailability(NO_AVAILABILITY);
			})
			.finally(() => {
				if (!controller.signal.aborted) setLoading(false);
			});

		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [
		year,
		month,
		occurrence,
		participationType,
		isRemoteParam,
		categoriesParam,
		tag,
		keyword,
		lat,
		lng,
		radius,
	]);

	return { availability, loading };
}
