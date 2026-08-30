import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { dispatchToast } from "../../lib/toastBus";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import { useApiClient } from "../../hooks/useApiClient";
import LocationSearchInput from "../LocationSearchInput";
import FilterDropdown, {
	DropdownOption,
	MultiDropdownOption,
} from "./FilterDropdown";
import MiniCalendar, { fmtShortDate } from "./MiniCalendar";
import OpportunityResultsList from "./OpportunityResultsList";
import { useVolunteerOpportunitiesData } from "./useVolunteerOpportunitiesData";
import {
	useOpportunityDateAvailability,
	type VisibleMonth,
} from "./useOpportunityDateAvailability";
import type { CitySuggestion } from "./useCitySuggestions";
import { resolveDateLocale } from "../../lib/format";
import {
	filterByLabelMatch,
	sortByLabelPrefixMatch,
} from "../../lib/citySuggestionSort";
import { SpinnerIcon } from "../Spinner";
import {
	BroomIcon,
	CalendarIcon,
	ClockIcon,
	CloseIcon,
	GlobeIcon,
	HashtagIcon,
	MagnifyingGlassIcon,
	MapPinIcon,
	TagIcon,
	UsersIcon,
} from "../icons";

const CATEGORY_VALUES = [
	"Social",
	"Environment",
	"Sport",
	"Education",
	"DisasterRelief",
	"Health",
	"Animals",
	"Culture",
	"Technology",
	"Other",
] as const;

const RADIUS_OPTIONS = [5, 10, 25, 50, 100];
const DEFAULT_RADIUS_KM = "10";
const NEAR_ME_TIMEOUT_MS = 10_000;
const NEAR_ME_MAX_AGE_MS = 60_000;

export default function VolunteerOpportunitiesList() {
	const { t, i18n } = useTranslation();
	const locale = resolveDateLocale(i18n.language);
	const api = useApiClient();
	const [searchParams, setSearchParams] = useSearchParams();

	const occurrence = searchParams.get("occurrence") ?? "";
	const participationType = searchParams.get("participationType") ?? "";
	const isRemoteParam = searchParams.get("isRemote") ?? "";
	const dateFrom = searchParams.get("dateFrom") ?? "";
	const dateTo = searchParams.get("dateTo") ?? "";
	const categoriesParam = searchParams.get("categories") ?? "";
	const tag = searchParams.get("tag") ?? "";
	const keyword = searchParams.get("q") ?? "";
	const city = searchParams.get("city") ?? "";
	const lat = searchParams.get("lat") ?? "";
	const lng = searchParams.get("lng") ?? "";
	const radius = searchParams.get("radius") ?? "";

	const selectedCategories = categoriesParam
		? categoriesParam.split(",").filter(Boolean)
		: [];
	const hasLocation = !!(lat && lng && radius);

	const [openFilter, setOpenFilter] = useState<string | null>(null);
	const [locationCityInput, setLocationCityInput] = useState(city);
	const [locationLoading, setLocationLoading] = useState(false);
	// A ?city= the geocoder cannot place filters nothing, so it must not read as an
	// applied filter - it used to render the same green chip (and summon "Reset") as a
	// real one while the full unfiltered list sat underneath it (#2319).
	const [cityUnresolved, setCityUnresolved] = useState(false);
	// Only this session's own geolocation fix may be labelled "near me". A shared URL
	// carries the sender's coordinates, and calling those the recipient's "near me" -
	// in the sender's language, at that - told them a location was theirs when it was
	// not (#2319).
	const [isOwnPosition, setIsOwnPosition] = useState(false);

	const filterBarRef = useDismissableOverlay<HTMLDivElement>(
		openFilter !== null,
		() => setOpenFilter(null),
	);

	useEffect(() => {
		if (!city || lat || lng) return;
		setCityUnresolved(false);
		const controller = new AbortController();
		(async () => {
			try {
				const places = await api.searchCities(city, controller.signal);
				const [best] = sortByLabelPrefixMatch(
					filterByLabelMatch(
						places.map((place) => ({
							label: place.label,
							lat: place.latitude,
							lng: place.longitude,
						})),
						city,
					),
					city,
				);
				if (!best) {
					setCityUnresolved(true);
					return;
				}
				const params = new URLSearchParams(window.location.search);
				params.set("city", best.label);
				params.set("lat", String(best.lat));
				params.set("lng", String(best.lng));
				params.set("radius", radius || DEFAULT_RADIUS_KM);
				setSearchParams(params, { replace: true });
			} catch {
				// A typo, an unmapped place or a transient geocoder failure: the city
				// filters nothing, so say so in the location panel rather than leaving
				// a chip that looks applied.
				if (!controller.signal.aborted) setCityUnresolved(true);
			}
		})();
		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [city, lat, lng]);

	const [visibleCalendarMonth, setVisibleCalendarMonth] =
		useState<VisibleMonth | null>(null);

	const handleVisibleMonthChange = useCallback(
		(year: number, month: number) => {
			setVisibleCalendarMonth((prev) =>
				prev?.year === year && prev.month === month ? prev : { year, month },
			);
		},
		[],
	);

	const { availability: dateAvailability, loading: dateAvailabilityLoading } =
		useOpportunityDateAvailability(
			openFilter === "date" ? visibleCalendarMonth : null,
			{
				occurrence,
				participationType,
				isRemoteParam,
				categoriesParam,
				tag,
				keyword,
				lat,
				lng,
				radius,
			},
		);

	const {
		items,
		loading,
		loadingMore,
		error,
		errorIsOffline,
		hasMore,
		loadMore,
		loadMoreError,
		loadMoreErrorIsOffline,
		retryLoadMore,
		totalItems,
	} = useVolunteerOpportunitiesData({
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
	});

	function updateFilter(key: string, value: string) {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (value) next.set(key, value);
				else next.delete(key);
				return next;
			},
			{ replace: true },
		);
	}

	function clearFilters() {
		setCityUnresolved(false);
		setIsOwnPosition(false);
		setLocationCityInput("");
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				next.delete("city");
				next.delete("lat");
				next.delete("lng");
				next.delete("radius");
				next.delete("occurrence");
				next.delete("participationType");
				next.delete("isRemote");
				next.delete("dateFrom");
				next.delete("dateTo");
				next.delete("categories");
				next.delete("tag");
				next.delete("q");
				return next;
			},
			{ replace: true },
		);
	}

	function clearLocation() {
		setCityUnresolved(false);
		setIsOwnPosition(false);
		setLocationCityInput("");
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				next.delete("city");
				next.delete("lat");
				next.delete("lng");
				next.delete("radius");
				return next;
			},
			{ replace: true },
		);
	}

	function clearDateRange() {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				next.delete("dateFrom");
				next.delete("dateTo");
				return next;
			},
			{ replace: true },
		);
	}

	function handleDateChange(from: string, to: string) {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (from) next.set("dateFrom", from);
				else next.delete("dateFrom");
				if (to) next.set("dateTo", to);
				else next.delete("dateTo");
				return next;
			},
			{ replace: true },
		);
	}

	function toggleCategory(cat: string) {
		const next = selectedCategories.includes(cat)
			? selectedCategories.filter((c) => c !== cat)
			: [...selectedCategories, cat];
		const params = new URLSearchParams(window.location.search);
		if (next.length > 0) params.set("categories", next.join(","));
		else params.delete("categories");
		setSearchParams(params, { replace: true });
	}

	function selectLocationSuggestion(suggestion: CitySuggestion) {
		setCityUnresolved(false);
		setIsOwnPosition(false);
		const currentRadius = searchParams.get("radius") || DEFAULT_RADIUS_KM;
		const params = new URLSearchParams(window.location.search);
		params.set("city", suggestion.label);
		params.set("lat", suggestion.lat.toString());
		params.set("lng", suggestion.lng.toString());
		params.set("radius", currentRadius);
		setSearchParams(params, { replace: true });
		setLocationCityInput(suggestion.label);
		setOpenFilter(null);
	}

	function handleNearMe() {
		if (!navigator.geolocation) {
			dispatchToast("error", t("opportunities.nearMeUnavailable"));
			return;
		}
		setLocationLoading(true);
		navigator.geolocation.getCurrentPosition(
			(pos) => {
				const { latitude, longitude } = pos.coords;
				const currentRadius = radius || DEFAULT_RADIUS_KM;
				const params = new URLSearchParams(window.location.search);
				// No city name to record - writing the translated "near me" label into
				// ?city= made it look like a place, and shipped the sender's language
				// and coordinates to whoever the URL was shared with (#2319).
				params.delete("city");
				params.set("lat", String(latitude));
				params.set("lng", String(longitude));
				params.set("radius", currentRadius);
				setSearchParams(params, { replace: true });
				setCityUnresolved(false);
				setIsOwnPosition(true);
				setLocationCityInput("");
				setOpenFilter(null);
				setLocationLoading(false);
			},
			(err) => {
				// getCurrentPosition has no default timeout, so a prompt that is never
				// answered used to leave the button disabled and spinning for good, with
				// nothing to retry from until the panel was closed and reopened (#2319).
				dispatchToast(
					"error",
					err.code === err.PERMISSION_DENIED
						? t("opportunities.nearMeDenied")
						: t("opportunities.nearMeUnavailable"),
				);
				setLocationLoading(false);
			},
			{ timeout: NEAR_ME_TIMEOUT_MS, maximumAge: NEAR_ME_MAX_AGE_MS },
		);
	}

	const hasFilters = !!(
		hasLocation ||
		occurrence ||
		participationType ||
		isRemoteParam ||
		dateFrom ||
		dateTo ||
		selectedCategories.length > 0 ||
		tag ||
		keyword
	);

	// A coordinate pair with no city name is either this session's own geolocation fix
	// or a set of coordinates someone shared; only the first is honestly "near me".
	const locationLabel =
		city ||
		(isOwnPosition
			? t("opportunities.nearMe")
			: t("opportunities.selectedLocation"));

	// An unresolved city is deliberately not shown as an applied value: it filters
	// nothing, so the chip stays inactive and the panel explains why.
	const locationDisplayValue = hasLocation
		? `${locationLabel} · ${radius} km`
		: "";

	const radiusDisabled = !lat || !lng;

	const categoryDisplayValue =
		selectedCategories.length === 0
			? ""
			: selectedCategories.length === 1
				? t(`opportunities.category.${selectedCategories[0]}`)
				: t("opportunities.nCategoriesSelected", {
						count: selectedCategories.length,
					});

	const dateDisplayValue = dateFrom
		? dateTo
			? t("opportunities.dateRangeDisplay", {
					from: fmtShortDate(dateFrom, locale),
					to: fmtShortDate(dateTo, locale),
				})
			: t("opportunities.dateFromDisplay", {
					date: fmtShortDate(dateFrom, locale),
				})
		: "";

	return (
		<div>
			<div ref={filterBarRef} className="mb-2">
				<div
					data-testid="opportunities-filter-bar"
					className="flex flex-wrap items-center gap-2 pb-3"
				>
					<FilterDropdown
						testId="filter-location"
						icon={<MapPinIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelLocation")}
						displayValue={locationDisplayValue}
						isOpen={openFilter === "location"}
						onToggle={() => {
							if (openFilter !== "location") {
								setLocationCityInput(city);
							}
							setOpenFilter((f) => (f === "location" ? null : "location"));
						}}
						onClear={clearLocation}
						clearAriaLabel={t("opportunities.clearLocation")}
						allowOverflow
					>
						<div className="w-72 p-4">
							<p className="mb-1.5 text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelCity")}
							</p>
							<div className="mb-3">
								<LocationSearchInput
									id="opportunities-location-search"
									value={locationCityInput}
									onValueChange={setLocationCityInput}
									onSelect={selectLocationSuggestion}
									placeholder={t("opportunities.locationPlaceholder")}
									ariaLabel={t("opportunities.filterLabelCity")}
									inputClassName="w-full rounded-xl border border-gray-200 bg-gray-50 py-2 pr-8 pl-9 text-sm text-gray-900 placeholder:text-gray-600 focus:border-brand-400 focus:bg-white"
								/>
								{cityUnresolved && !hasLocation && (
									<p
										role="status"
										data-testid="opportunities-city-unresolved"
										className="mt-1.5 text-xs text-amber-700"
									>
										{t("opportunities.cityUnresolved", { city })}
									</p>
								)}
							</div>

							<button
								type="button"
								onClick={handleNearMe}
								disabled={locationLoading}
								aria-label={t("opportunities.nearMe")}
								className="mb-3 flex w-full items-center justify-center gap-2 rounded-lg border border-gray-500 bg-gray-50 py-2 text-sm text-gray-600 transition-colors hover:border-brand-600 hover:bg-brand-50 hover:text-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{locationLoading ? (
									<SpinnerIcon className="h-4 w-4" />
								) : (
									<MapPinIcon className="h-4 w-4" />
								)}
								{t("opportunities.nearMe")}
							</button>

							<p className="mb-1.5 text-xs font-medium text-gray-500">
								{t("opportunities.radiusLabel")}
							</p>
							<div className="flex flex-wrap gap-1.5">
								{RADIUS_OPTIONS.map((r) => (
									<button
										key={r}
										type="button"
										onClick={() => updateFilter("radius", String(r))}
										aria-pressed={radius === String(r)}
										disabled={radiusDisabled}
										aria-describedby={
											radiusDisabled ? "opportunities-radius-hint" : undefined
										}
										className={`rounded-full border px-3 py-1 text-sm transition-colors disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:border-gray-200 ${
											radius === String(r)
												? "border-brand-600 bg-brand-50 font-medium text-brand-700"
												: "border-gray-500 text-gray-600 hover:border-gray-600"
										}`}
									>
										{t("opportunities.radiusKmValue", { count: r })}
									</button>
								))}
							</div>

							{radiusDisabled && (
								<p
									id="opportunities-radius-hint"
									className="mt-1.5 text-xs text-gray-500"
								>
									{t("opportunities.radiusRequiresLocation")}
								</p>
							)}
						</div>
					</FilterDropdown>

					<FilterDropdown
						icon={<TagIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelCategory")}
						displayValue={categoryDisplayValue}
						isOpen={openFilter === "category"}
						onToggle={() =>
							setOpenFilter((f) => (f === "category" ? null : "category"))
						}
						onClear={() => {
							const params = new URLSearchParams(window.location.search);
							params.delete("categories");
							setSearchParams(params, { replace: true });
						}}
						clearAriaLabel={t("opportunities.clearCategory")}
					>
						<div className="py-1">
							{CATEGORY_VALUES.map((c) => (
								<MultiDropdownOption
									key={c}
									label={t(`opportunities.category.${c}`)}
									selected={selectedCategories.includes(c)}
									onClick={() => toggleCategory(c)}
								/>
							))}
						</div>
					</FilterDropdown>

					<FilterDropdown
						testId="filter-type"
						icon={<UsersIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelType")}
						displayValue={
							participationType === "ScheduledSlots"
								? t("opportunities.waitlist")
								: participationType === "IndividualContact"
									? t("opportunities.individualContact")
									: ""
						}
						isOpen={openFilter === "type"}
						onToggle={() =>
							setOpenFilter((f) => (f === "type" ? null : "type"))
						}
						onClear={() => updateFilter("participationType", "")}
						clearAriaLabel={t("opportunities.clearType")}
					>
						<DropdownOption
							label={t("opportunities.all")}
							selected={!participationType}
							onClick={() => {
								updateFilter("participationType", "");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.waitlist")}
							selected={participationType === "ScheduledSlots"}
							onClick={() => {
								updateFilter("participationType", "ScheduledSlots");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.individualContact")}
							selected={participationType === "IndividualContact"}
							onClick={() => {
								updateFilter("participationType", "IndividualContact");
								setOpenFilter(null);
							}}
						/>
					</FilterDropdown>

					<FilterDropdown
						icon={<GlobeIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelRemote")}
						displayValue={
							isRemoteParam === "true"
								? t("opportunities.remote")
								: isRemoteParam === "false"
									? t("opportunities.onsite")
									: ""
						}
						isOpen={openFilter === "remote"}
						onToggle={() =>
							setOpenFilter((f) => (f === "remote" ? null : "remote"))
						}
						onClear={() => updateFilter("isRemote", "")}
						clearAriaLabel={t("opportunities.clearLocation")}
					>
						<DropdownOption
							label={t("opportunities.all")}
							selected={!isRemoteParam}
							onClick={() => {
								updateFilter("isRemote", "");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.remote")}
							selected={isRemoteParam === "true"}
							onClick={() => {
								updateFilter("isRemote", "true");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.onsite")}
							selected={isRemoteParam === "false"}
							onClick={() => {
								updateFilter("isRemote", "false");
								setOpenFilter(null);
							}}
						/>
					</FilterDropdown>

					<FilterDropdown
						testId="filter-frequency"
						icon={<ClockIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelFrequency")}
						displayValue={
							occurrence === "OneTime"
								? t("opportunities.oneTime")
								: occurrence === "Recurring"
									? t("opportunities.recurring")
									: ""
						}
						isOpen={openFilter === "frequency"}
						onToggle={() =>
							setOpenFilter((f) => (f === "frequency" ? null : "frequency"))
						}
						onClear={() => updateFilter("occurrence", "")}
						clearAriaLabel={t("opportunities.clearOccurrence")}
					>
						<DropdownOption
							label={t("opportunities.all")}
							selected={!occurrence}
							onClick={() => {
								updateFilter("occurrence", "");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.oneTime")}
							selected={occurrence === "OneTime"}
							onClick={() => {
								updateFilter("occurrence", "OneTime");
								setOpenFilter(null);
							}}
						/>
						<DropdownOption
							label={t("opportunities.recurring")}
							selected={occurrence === "Recurring"}
							onClick={() => {
								updateFilter("occurrence", "Recurring");
								setOpenFilter(null);
							}}
						/>
					</FilterDropdown>

					<FilterDropdown
						icon={<CalendarIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelDateRange")}
						displayValue={dateDisplayValue}
						isOpen={openFilter === "date"}
						onToggle={() =>
							setOpenFilter((f) => (f === "date" ? null : "date"))
						}
						onClear={clearDateRange}
						clearAriaLabel={t("opportunities.clearDateRange")}
					>
						<MiniCalendar
							fromStr={dateFrom}
							toStr={dateTo}
							onChange={handleDateChange}
							availability={dateAvailability}
							availabilityLoading={dateAvailabilityLoading}
							onVisibleMonthChange={handleVisibleMonthChange}
						/>
					</FilterDropdown>

					{keyword && (
						<div
							role="group"
							aria-label={`${t("opportunities.filterLabelKeyword")}: ${keyword}`}
							className="inline-flex items-stretch overflow-hidden rounded-full border border-brand-600 bg-white"
						>
							<span className="flex items-center gap-1.5 py-1.5 pr-1.5 pl-3 text-sm font-medium whitespace-nowrap text-brand-700">
								<MagnifyingGlassIcon className="h-3.5 w-3.5 shrink-0 text-brand-600" />
								<span>&quot;{keyword}&quot;</span>
							</span>
							<button
								type="button"
								onClick={() => updateFilter("q", "")}
								aria-label={t("opportunities.clearKeyword")}
								className="flex items-center px-2 py-1.5 text-brand-700 transition-colors hover:bg-brand-100 hover:text-brand-800"
							>
								<CloseIcon />
							</button>
						</div>
					)}

					{tag && (
						<div
							role="group"
							aria-label={`${t("opportunities.filterLabelTag")}: ${tag}`}
							className="inline-flex items-stretch overflow-hidden rounded-full border border-brand-600 bg-white"
						>
							<span className="flex items-center gap-1.5 py-1.5 pr-1.5 pl-3 text-sm font-medium whitespace-nowrap text-brand-700">
								<HashtagIcon className="h-3.5 w-3.5 shrink-0 text-brand-600" />
								<span>#{tag}</span>
							</span>
							<button
								type="button"
								onClick={() => updateFilter("tag", "")}
								aria-label={t("opportunities.clearTag")}
								className="flex items-center px-2 py-1.5 text-brand-700 transition-colors hover:bg-brand-100 hover:text-brand-800"
							>
								<CloseIcon />
							</button>
						</div>
					)}

					{hasFilters && (
						<button
							type="button"
							onClick={clearFilters}
							className="flex items-center gap-1.5 rounded-full border border-brand-600 bg-brand-50 px-3 py-1.5 text-sm font-medium text-brand-700 transition-colors hover:bg-brand-100"
						>
							<BroomIcon />
							{t("opportunities.clearFilters")}
						</button>
					)}
				</div>
			</div>

			<OpportunityResultsList
				loading={loading}
				error={error}
				errorIsOffline={errorIsOffline}
				items={items}
				totalItems={totalItems}
				hasFilters={hasFilters}
				onClearFilters={clearFilters}
				hasMore={hasMore}
				loadingMore={loadingMore}
				onLoadMore={loadMore}
				loadMoreError={loadMoreError}
				loadMoreErrorIsOffline={loadMoreErrorIsOffline}
				onRetryLoadMore={retryLoadMore}
				keyword={keyword}
			/>
		</div>
	);
}
