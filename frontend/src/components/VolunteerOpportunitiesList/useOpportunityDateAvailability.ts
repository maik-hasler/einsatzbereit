import { useEffect, useState } from "react";
import { useApiClient } from "../../hooks/useApiClient";
import { fetchVolunteerOpportunityDateAvailability } from "../../lib/volunteerOpportunities";
import type { VolunteerOpportunitiesFilters } from "./useVolunteerOpportunitiesData";

export interface VisibleMonth {
	year: number;
	/** 0-11, matching `Date.prototype.getMonth()`. */
	month: number;
}

// The date range is the one filter this deliberately drops: the calendar is being
// asked which days are worth picking, so scoping the answer to the range already
// picked would only ever confirm itself and mark nothing else (#1779).
export type OpportunityDateAvailabilityFilters = Omit<
	VolunteerOpportunitiesFilters,
	"dateFrom" | "dateTo"
>;

const NO_AVAILABILITY: ReadonlyMap<string, number> = new Map();

/**
 * How many opportunities each day of `visibleMonth` has, keyed by local ISO date
 * (`YYYY-MM-DD`) so the day cells can look themselves up without re-deriving a
 * calendar day from a timestamp. Returns an empty map while the date popover is
 * closed (`visibleMonth` null) - nothing is asked of the backend until a visitor
 * actually opens the calendar.
 */
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

		// Local midnight to local end-of-day, the same day boundaries the grid draws -
		// `new Date(year, month + 1, 0)` is the last day of `month`, not the next one.
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
				// getTimezoneOffset() counts minutes *behind* UTC (Berlin in summer
				// returns -120), the opposite sign of the "+02:00" the API asks for.
				// Read off `from` rather than today so a month on the far side of a DST
				// change is bucketed with its own offset, not the current one.
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
				// The marks are a hint on top of a calendar that still works without
				// them, so a failed lookup degrades to an unmarked grid rather than an
				// error the visitor has to get past before picking a date.
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
