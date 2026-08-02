import { useEffect, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { dispatchToast } from "../../lib/toastBus";
import FilterDropdown, {
	DropdownOption,
	MultiDropdownOption,
} from "./FilterDropdown";
import MiniCalendar, { fmtShortDate } from "./MiniCalendar";
import OpportunityResultsList from "./OpportunityResultsList";
import { useVolunteerOpportunitiesData } from "./useVolunteerOpportunitiesData";
import { useCitySuggestions, type CitySuggestion } from "./useCitySuggestions";
import { resolveDateLocale } from "../../lib/format";
import { SpinnerIcon } from "../Spinner";
import {
	BroomIcon,
	CalendarIcon,
	ChipXIcon,
	ClockIcon,
	GlobeIcon,
	HashIcon,
	PinIcon,
	TagIcon,
	UsersIcon,
} from "./icons";

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

export default function VolunteerOpportunitiesList() {
	const { t, i18n } = useTranslation();
	const locale = resolveDateLocale(i18n.language);
	const [searchParams, setSearchParams] = useSearchParams();

	const occurrence = searchParams.get("occurrence") ?? "";
	const participationType = searchParams.get("participationType") ?? "";
	const isRemoteParam = searchParams.get("isRemote") ?? "";
	const dateFrom = searchParams.get("dateFrom") ?? "";
	const dateTo = searchParams.get("dateTo") ?? "";
	const categoriesParam = searchParams.get("categories") ?? "";
	const tag = searchParams.get("tag") ?? "";
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
	const [activeSuggestionIndex, setActiveSuggestionIndex] = useState(-1);
	const locationListboxId = "location-suggestions-listbox";

	const {
		suggestions: locationSuggestions,
		show: showLocationSuggestions,
		setShow: setShowLocationSuggestions,
		reset: resetLocationSuggestions,
	} = useCitySuggestions(locationCityInput);

	// Keeps the roving highlight in bounds (and cleared) whenever the
	// suggestion list itself changes - a stale index from the previous
	// keystroke's results would otherwise point at the wrong (or a
	// no-longer-existing) option.
	useEffect(() => {
		setActiveSuggestionIndex(-1);
	}, [locationSuggestions]);

	function handleLocationInputKeyDown(e: KeyboardEvent<HTMLInputElement>) {
		if (!showLocationSuggestions || locationSuggestions.length === 0) return;
		switch (e.key) {
			case "ArrowDown":
				e.preventDefault();
				setActiveSuggestionIndex((i) => (i + 1) % locationSuggestions.length);
				break;
			case "ArrowUp":
				e.preventDefault();
				setActiveSuggestionIndex(
					(i) =>
						(i - 1 + locationSuggestions.length) % locationSuggestions.length,
				);
				break;
			case "Enter":
				if (activeSuggestionIndex >= 0) {
					e.preventDefault();
					selectLocationSuggestion(locationSuggestions[activeSuggestionIndex]);
				}
				break;
			case "Escape":
				e.preventDefault();
				setShowLocationSuggestions(false);
				setActiveSuggestionIndex(-1);
				break;
		}
	}

	const filterBarRef = useRef<HTMLDivElement>(null);

	const {
		items,
		loading,
		loadingMore,
		error,
		hasMore,
		loadMore,
		loadMoreError,
		retryLoadMore,
	} = useVolunteerOpportunitiesData({
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
	});

	useEffect(() => {
		function handleOutside(e: MouseEvent) {
			if (
				filterBarRef.current &&
				!filterBarRef.current.contains(e.target as Node)
			) {
				setOpenFilter(null);
			}
		}
		document.addEventListener("mousedown", handleOutside);
		return () => document.removeEventListener("mousedown", handleOutside);
	}, []);

	function updateFilter(key: string, value: string) {
		const next = new URLSearchParams(window.location.search);
		if (value) next.set(key, value);
		else next.delete(key);
		setSearchParams(next, { replace: true });
	}

	function clearFilters() {
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
				return next;
			},
			{ replace: true },
		);
	}

	function clearLocation() {
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
		const currentRadius = searchParams.get("radius") || "10";
		const params = new URLSearchParams(window.location.search);
		params.set("city", suggestion.label);
		params.set("lat", suggestion.lat.toString());
		params.set("lng", suggestion.lng.toString());
		params.set("radius", currentRadius);
		setSearchParams(params, { replace: true });
		setLocationCityInput(suggestion.label);
		setShowLocationSuggestions(false);
		setOpenFilter(null);
	}

	function handleNearMe() {
		if (!navigator.geolocation) {
			dispatchToast("error", t("opportunities.nearMeDenied"));
			return;
		}
		setLocationLoading(true);
		navigator.geolocation.getCurrentPosition(
			(pos) => {
				const { latitude, longitude } = pos.coords;
				const currentRadius = radius || "10";
				const label = t("opportunities.nearMe");
				const params = new URLSearchParams(window.location.search);
				params.set("city", label);
				params.set("lat", String(latitude));
				params.set("lng", String(longitude));
				params.set("radius", currentRadius);
				setSearchParams(params, { replace: true });
				setLocationCityInput(label);
				setShowLocationSuggestions(false);
				setOpenFilter(null);
				setLocationLoading(false);
			},
			() => {
				dispatchToast("error", t("opportunities.nearMeDenied"));
				setLocationLoading(false);
			},
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
		tag
	);

	const locationDisplayValue = hasLocation ? `${city} · ${radius} km` : "";

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
			<div className="mb-8 text-center">
				<h2 className="text-2xl font-bold text-gray-900 sm:text-3xl">
					{t("opportunities.currentNeeds")}
				</h2>
				<p className="mx-auto mt-3 max-w-xl text-sm leading-relaxed text-gray-500 sm:text-base">
					{t("opportunities.subtitle")}
				</p>
			</div>

			{/* Filter bar */}
			<div ref={filterBarRef} className="mb-2">
				<div className="flex flex-wrap items-center justify-center gap-2 pb-3">
					{/* Location + Radius */}
					<FilterDropdown
						icon={<PinIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelLocation")}
						displayValue={locationDisplayValue}
						isOpen={openFilter === "location"}
						onToggle={() => {
							if (openFilter !== "location") {
								setLocationCityInput(city);
								resetLocationSuggestions();
							}
							setOpenFilter((f) => (f === "location" ? null : "location"));
						}}
						onClear={clearLocation}
						clearAriaLabel={t("opportunities.clearLocation")}
					>
						<div className="w-72 p-4">
							<p className="mb-1.5 text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelCity")}
							</p>
							<div className="relative mb-3">
								<PinIcon className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-gray-400" />
								<input
									type="text"
									role="combobox"
									aria-label={t("opportunities.filterLabelCity")}
									aria-expanded={showLocationSuggestions}
									aria-controls={locationListboxId}
									aria-autocomplete="list"
									aria-activedescendant={
										showLocationSuggestions && activeSuggestionIndex >= 0
											? `${locationListboxId}-option-${activeSuggestionIndex}`
											: undefined
									}
									placeholder={t("opportunities.locationPlaceholder")}
									value={locationCityInput}
									onChange={(e) => setLocationCityInput(e.target.value)}
									onKeyDown={handleLocationInputKeyDown}
									onBlur={() =>
										setTimeout(() => setShowLocationSuggestions(false), 150)
									}
									onFocus={() => {
										if (locationSuggestions.length > 0)
											setShowLocationSuggestions(true);
									}}
									className="w-full rounded-lg border border-gray-200 bg-gray-50 py-2 pr-8 pl-9 text-sm text-gray-900 placeholder:text-gray-400 focus:border-brand-500 focus:bg-white"
								/>
								{locationCityInput && (
									<button
										type="button"
										onClick={() => {
											setLocationCityInput("");
											resetLocationSuggestions();
										}}
										aria-label={t("opportunities.clearCity")}
										className="absolute top-1/2 right-2.5 -translate-y-1/2 text-gray-400 hover:text-gray-600"
									>
										&times;
									</button>
								)}
								{showLocationSuggestions && (
									<ul
										id={locationListboxId}
										role="listbox"
										aria-label={t("opportunities.filterLabelCity")}
										className="absolute top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-gray-200 bg-white shadow-modal"
									>
										{locationSuggestions.map((s, i) => (
											<li
												key={i}
												id={`${locationListboxId}-option-${i}`}
												role="option"
												aria-selected={i === activeSuggestionIndex}
												onMouseDown={(e) => e.preventDefault()}
												onMouseEnter={() => setActiveSuggestionIndex(i)}
												onClick={() => selectLocationSuggestion(s)}
												// Keyboard selection normally goes through the input's
												// own onKeyDown (aria-activedescendant combobox
												// pattern - this option is never itself focused), but
												// jsx-a11y/click-events-have-key-events still requires
												// a click element to carry its own key handler too.
												onKeyDown={(e) => {
													if (e.key === "Enter") selectLocationSuggestion(s);
												}}
												className={`cursor-pointer px-3 py-2 text-sm text-gray-700 ${
													i === activeSuggestionIndex
														? "bg-brand-50 text-brand-700"
														: "hover:bg-brand-50 hover:text-brand-700"
												}`}
											>
												<span className="flex items-center gap-2">
													<PinIcon className="h-3.5 w-3.5 shrink-0 text-gray-400" />
													{s.label}
												</span>
											</li>
										))}
									</ul>
								)}
							</div>

							<button
								type="button"
								onClick={handleNearMe}
								disabled={locationLoading}
								aria-label={t("opportunities.nearMe")}
								className="mb-3 flex w-full items-center justify-center gap-2 rounded-lg border border-gray-200 bg-gray-50 py-2 text-sm text-gray-600 transition-colors hover:border-brand-300 hover:bg-brand-50 hover:text-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{locationLoading ? (
									<SpinnerIcon className="h-4 w-4" />
								) : (
									<PinIcon className="h-4 w-4" />
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
										className={`rounded-full border px-3 py-1 text-sm transition-colors ${
											radius === String(r)
												? "border-brand-500 bg-brand-50 font-medium text-brand-700"
												: "border-gray-200 text-gray-600 hover:border-gray-300"
										}`}
									>
										{t("opportunities.radiusKmValue", { count: r })}
									</button>
								))}
							</div>
						</div>
					</FilterDropdown>

					{/* Category multi-select */}
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

					{/* Participation type */}
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

					{/* Remote / onsite */}
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

					{/* Frequency */}
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

					{/* Date range - custom calendar picker */}
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
						/>
					</FilterDropdown>

					{/* Tag (static pill) */}
					{tag && (
						<div
							role="group"
							aria-label={`${t("opportunities.filterLabelTag")}: ${tag}`}
							className="inline-flex items-stretch overflow-hidden rounded-full border border-brand-500 bg-brand-50"
						>
							<span className="flex items-center gap-1.5 py-1.5 pr-1.5 pl-3 text-sm font-medium whitespace-nowrap text-brand-700">
								<HashIcon
									className="h-3.5 w-3.5 shrink-0 text-brand-500"
									aria-hidden="true"
								/>
								<span>#{tag}</span>
							</span>
							<button
								type="button"
								onClick={() => updateFilter("tag", "")}
								aria-label={t("opportunities.clearTag")}
								className="flex items-center px-2 py-1.5 text-brand-400 transition-colors hover:bg-brand-100 hover:text-brand-600"
							>
								<ChipXIcon />
							</button>
						</div>
					)}

					{hasFilters && (
						<button
							type="button"
							onClick={clearFilters}
							className="flex items-center gap-1.5 rounded-full border border-red-200 bg-red-50 px-3 py-1.5 text-sm font-medium text-red-600 transition-colors hover:border-red-300 hover:bg-red-100"
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
				items={items}
				hasFilters={hasFilters}
				onClearFilters={clearFilters}
				hasMore={hasMore}
				loadingMore={loadingMore}
				onLoadMore={loadMore}
				loadMoreError={loadMoreError}
				onRetryLoadMore={retryLoadMore}
			/>
		</div>
	);
}
