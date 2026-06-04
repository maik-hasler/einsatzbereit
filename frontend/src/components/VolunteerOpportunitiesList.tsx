import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import { getApiErrorMessage } from "../lib/apiError";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import EmptyState from "./EmptyState";
import OpportunityMap from "./OpportunityMap";
import {
	useOpportunityViewFilters,
	type OpportunityBounds,
} from "../hooks/useOpportunityFilters";

interface Props {
	canCreateOpportunity: boolean;
}

const LIST_PAGE_SIZE = 10;
const MAP_PAGE_SIZE = 200;

function ChipCloseIcon() {
	return (
		<svg
			className="h-3 w-3"
			viewBox="0 0 12 12"
			fill="currentColor"
			aria-hidden="true"
		>
			<path d="M6 4.586 10.586 0 12 1.414 7.414 6 12 10.586 10.586 12 6 7.414 1.414 12 0 10.586 4.586 6 0 1.414 1.414 0 6 4.586z" />
		</svg>
	);
}

function FilterChip({
	label,
	ariaLabel,
	onRemove,
}: {
	label: string;
	ariaLabel: string;
	onRemove: () => void;
}) {
	return (
		<span className="inline-flex items-center gap-1 rounded-full bg-brand-100 px-2.5 py-1 text-xs font-medium text-brand-800">
			{label}
			<button
				type="button"
				onClick={onRemove}
				aria-label={ariaLabel}
				className="rounded-full p-0.5 hover:bg-brand-200"
			>
				<ChipCloseIcon />
			</button>
		</span>
	);
}

function FilterSelect({
	fieldLabel,
	ariaLabel,
	value,
	onChange,
	children,
}: {
	fieldLabel: string;
	ariaLabel: string;
	value: string;
	onChange: (v: string) => void;
	children: React.ReactNode;
}) {
	const active = !!value;
	return (
		<div className="flex flex-col gap-1">
			<span className="text-xs font-medium text-gray-500">{fieldLabel}</span>
			<div className="relative">
				<select
					aria-label={ariaLabel}
					value={value}
					onChange={(e) => onChange(e.target.value)}
					className={`appearance-none cursor-pointer rounded-lg border py-2 pl-3 pr-8 text-sm font-medium transition-colors focus:outline-none ${
						active
							? "border-brand-500 bg-brand-50 text-brand-700"
							: "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
					}`}
				>
					{children}
				</select>
				<svg
					className={`pointer-events-none absolute right-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 ${active ? "text-brand-500" : "text-gray-400"}`}
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2.5"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="m19.5 8.25-7.5 7.5-7.5-7.5"
					/>
				</svg>
			</div>
		</div>
	);
}

export default function VolunteerOpportunitiesList({
	canCreateOpportunity,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [searchParams, setSearchParams] = useSearchParams();
	const search = searchParams.get("search") ?? "";
	const city = searchParams.get("city") ?? "";
	const occurrence = searchParams.get("occurrence") ?? "";
	const participationType = searchParams.get("participationType") ?? "";
	const isRemoteParam = searchParams.get("isRemote") ?? "";
	const dateFrom = searchParams.get("dateFrom") ?? "";
	const dateTo = searchParams.get("dateTo") ?? "";
	const category = searchParams.get("category") ?? "";
	const tag = searchParams.get("tag") ?? "";

	const [searchInput, setSearchInput] = useState(search);
	const [cityInput, setCityInput] = useState(city);
	const [filtersOpen, setFiltersOpen] = useState(false);

	const searchDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
	const cityDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

	const activeFilterCount = [
		search,
		city,
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		category,
		tag,
	].filter(Boolean).length;

	const { view, bounds, setView, setBounds } = useOpportunityViewFilters();
	const isMap = view === "map";

	const [items, setItems] = useState<VolunteerOpportunitySummary[]>([]);
	const [page, setPage] = useState(1);
	const [pageCount, setPageCount] = useState(1);
	const [loading, setLoading] = useState(true);
	const [loadingMore, setLoadingMore] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [refreshKey, setRefreshKey] = useState(0);
	const [showModal, setShowModal] = useState(false);

	useEffect(() => {
		setSearchInput(search);
	}, [search]);

	useEffect(() => {
		setCityInput(city);
	}, [city]);

	const prevFiltersRef = useRef({
		search,
		city,
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		category,
		tag,
		isMap,
		refreshKey,
		bn: bounds?.north,
		bs: bounds?.south,
		be: bounds?.east,
		bw: bounds?.west,
	});

	useEffect(() => {
		const prev = prevFiltersRef.current;
		const filterChanged =
			prev.search !== search ||
			prev.city !== city ||
			prev.occurrence !== occurrence ||
			prev.participationType !== participationType ||
			prev.isRemoteParam !== isRemoteParam ||
			prev.dateFrom !== dateFrom ||
			prev.dateTo !== dateTo ||
			prev.category !== category ||
			prev.tag !== tag ||
			prev.isMap !== isMap ||
			prev.bn !== bounds?.north ||
			prev.bs !== bounds?.south ||
			prev.be !== bounds?.east ||
			prev.bw !== bounds?.west ||
			prev.refreshKey !== refreshKey;

		prevFiltersRef.current = {
			search,
			city,
			occurrence,
			participationType,
			isRemoteParam,
			dateFrom,
			dateTo,
			category,
			tag,
			isMap,
			refreshKey,
			bn: bounds?.north,
			bs: bounds?.south,
			be: bounds?.east,
			bw: bounds?.west,
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

		const mapBounds = isMap ? bounds : undefined;

		let cancelled = false;
		const isRemoteBool =
			isRemoteParam === "true"
				? true
				: isRemoteParam === "false"
					? false
					: undefined;
		const dateFromParsed = dateFrom ? new Date(dateFrom) : undefined;
		const dateToParsed = dateTo ? new Date(dateTo) : undefined;

		api
			.getVolunteerOpportunities(
				page,
				isMap ? MAP_PAGE_SIZE : LIST_PAGE_SIZE,
				search || undefined,
				city || undefined,
				occurrence || undefined,
				participationType || undefined,
				isRemoteBool,
				dateFromParsed,
				dateToParsed,
				mapBounds?.north,
				mapBounds?.south,
				mapBounds?.east,
				mapBounds?.west,
				undefined,
				undefined,
				undefined,
				category || undefined,
				tag || undefined,
			)
			.then((result) => {
				if (cancelled) return;
				if (page === 1) setItems(result.items);
				else setItems((prev) => [...prev, ...result.items]);
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
		search,
		city,
		occurrence,
		participationType,
		isRemoteParam,
		dateFrom,
		dateTo,
		category,
		tag,
		isMap,
		bounds?.north,
		bounds?.south,
		bounds?.east,
		bounds?.west,
		refreshKey,
	]);

	const activeOrgId = getActiveOrgId();

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
				next.delete("search");
				next.delete("city");
				next.delete("occurrence");
				next.delete("participationType");
				next.delete("isRemote");
				next.delete("dateFrom");
				next.delete("dateTo");
				next.delete("category");
				next.delete("tag");
				return next;
			},
			{ replace: true },
		);
	}

	const hasFilters = !!(
		search ||
		city ||
		occurrence ||
		participationType ||
		isRemoteParam ||
		dateFrom ||
		dateTo ||
		category ||
		tag
	);

	function handleBoundsChange(next: OpportunityBounds) {
		setBounds(next);
	}

	const dateInputClass = (active: boolean) =>
		`rounded-lg border py-2 pl-3 pr-3 text-sm focus:outline-none ${
			active
				? "border-brand-500 bg-brand-50 text-brand-700"
				: "border-gray-200 bg-white text-gray-600"
		}`;

	return (
		<div>
			<div className="mb-4 flex items-center justify-between">
				<h2 className="text-xl font-semibold text-gray-900">
					{t("opportunities.currentNeeds")}
				</h2>
				<div className="flex items-center gap-2">
					<div
						role="group"
						className="inline-flex overflow-hidden rounded-lg border border-gray-200"
					>
						<button
							type="button"
							data-testid="view-toggle-list"
							aria-pressed={!isMap}
							onClick={() => setView("list")}
							className={
								!isMap
									? "bg-brand-800 px-3 py-1.5 text-sm font-medium text-white"
									: "px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-brand-50 hover:text-brand-700"
							}
						>
							{t("opportunities.view.list")}
						</button>
						<button
							type="button"
							data-testid="view-toggle-map"
							aria-pressed={isMap}
							onClick={() => setView("map")}
							className={
								isMap
									? "bg-brand-800 px-3 py-1.5 text-sm font-medium text-white"
									: "px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-brand-50 hover:text-brand-700"
							}
						>
							{t("opportunities.view.map")}
						</button>
					</div>
					{canCreateOpportunity && (
						<button
							onClick={() => setShowModal(true)}
							data-testid="create-opportunity-btn"
							className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 focus:outline-none"
						>
							{t("opportunities.createNeed")}
						</button>
					)}
				</div>
			</div>

			<div className="mb-4 rounded-xl border border-gray-200 bg-white shadow-sm">
				{/* Search + city row - always visible */}
				<div className="p-4 pb-3">
					<div className="flex flex-col gap-3 sm:flex-row">
						<div className="flex flex-1 flex-col gap-1">
							<span className="text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelSearch")}
							</span>
							<div className="relative">
								<svg
									className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="2"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
									/>
								</svg>
								<input
									type="text"
									aria-label={t("opportunities.searchPlaceholder")}
									placeholder={t("opportunities.searchPlaceholder")}
									value={searchInput}
									onChange={(e) => {
										const val = e.target.value;
										setSearchInput(val);
										if (searchDebounceRef.current)
											clearTimeout(searchDebounceRef.current);
										searchDebounceRef.current = setTimeout(() => {
											updateFilter("search", val);
										}, 400);
									}}
									className="w-full rounded-lg border border-gray-200 bg-gray-50 py-2 pl-9 pr-8 text-sm text-gray-900 placeholder:text-gray-400 focus:border-brand-500 focus:bg-white focus:outline-none"
								/>
								{searchInput && (
									<button
										type="button"
										onClick={() => {
											setSearchInput("");
											updateFilter("search", "");
										}}
										aria-label={t("opportunities.clearSearch")}
										className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
									>
										&times;
									</button>
								)}
							</div>
						</div>
						<div className="flex flex-col gap-1 sm:w-44">
							<span className="text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelCity")}
							</span>
							<div className="relative">
								<svg
									className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="2"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
									/>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
									/>
								</svg>
								<input
									type="text"
									aria-label={t("opportunities.cityPlaceholder")}
									placeholder={t("opportunities.cityPlaceholder")}
									value={cityInput}
									onChange={(e) => {
										const val = e.target.value;
										setCityInput(val);
										if (cityDebounceRef.current)
											clearTimeout(cityDebounceRef.current);
										cityDebounceRef.current = setTimeout(() => {
											updateFilter("city", val);
										}, 400);
									}}
									className="w-full rounded-lg border border-gray-200 bg-gray-50 py-2 pl-9 pr-8 text-sm text-gray-900 placeholder:text-gray-400 focus:border-brand-500 focus:bg-white focus:outline-none"
								/>
								{cityInput && (
									<button
										type="button"
										onClick={() => {
											setCityInput("");
											updateFilter("city", "");
										}}
										aria-label={t("opportunities.clearCity")}
										className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
									>
										&times;
									</button>
								)}
							</div>
						</div>
					</div>
				</div>

				{/* Additional filters - collapsible on mobile */}
				<div className="border-t border-gray-100">
					<button
						type="button"
						onClick={() => setFiltersOpen((o) => !o)}
						className="flex w-full items-center justify-between px-4 py-3 text-sm font-medium text-gray-600 hover:bg-gray-50 sm:hidden"
					>
						<span className="flex items-center gap-2">
							<svg
								className="h-4 w-4 text-brand-600"
								fill="none"
								viewBox="0 0 24 24"
								strokeWidth="2"
								stroke="currentColor"
								aria-hidden="true"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="M3 4.5h14.25M3 9h9.75M3 13.5h5.25"
								/>
							</svg>
							{t("opportunities.filterToggle")}
							{activeFilterCount > 0 && (
								<span className="rounded-full bg-brand-500 px-1.5 py-0.5 text-xs font-medium text-white">
									{activeFilterCount}
								</span>
							)}
						</span>
						<svg
							className={`h-4 w-4 text-gray-400 transition-transform ${filtersOpen ? "rotate-180" : ""}`}
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="2"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="m19.5 8.25-7.5 7.5-7.5-7.5"
							/>
						</svg>
					</button>

					<div
						className={`flex flex-wrap items-end gap-3 px-4 pb-4 pt-3 ${filtersOpen ? "" : "max-sm:hidden"}`}
					>
						<FilterSelect
							fieldLabel={t("opportunities.filterLabelCategory")}
							ariaLabel={t("opportunities.allCategories")}
							value={category}
							onChange={(v) => updateFilter("category", v)}
						>
							<option value="">{t("opportunities.allCategories")}</option>
							{(
								[
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
								] as const
							).map((c) => (
								<option key={c} value={c}>
									{t(`opportunities.category.${c}`)}
								</option>
							))}
						</FilterSelect>

						<FilterSelect
							fieldLabel={t("opportunities.filterLabelType")}
							ariaLabel={t("opportunities.allTypes")}
							value={participationType}
							onChange={(v) => updateFilter("participationType", v)}
						>
							<option value="">{t("opportunities.allTypes")}</option>
							<option value="Waitlist">{t("opportunities.waitlist")}</option>
							<option value="IndividualContact">
								{t("opportunities.individualContact")}
							</option>
						</FilterSelect>

						<FilterSelect
							fieldLabel={t("opportunities.filterLabelLocation")}
							ariaLabel={t("opportunities.allLocations")}
							value={isRemoteParam}
							onChange={(v) => updateFilter("isRemote", v)}
						>
							<option value="">{t("opportunities.allLocations")}</option>
							<option value="true">{t("opportunities.remote")}</option>
							<option value="false">{t("opportunities.onsite")}</option>
						</FilterSelect>

						<FilterSelect
							fieldLabel={t("opportunities.filterLabelFrequency")}
							ariaLabel={t("opportunities.allFrequencies")}
							value={occurrence}
							onChange={(v) => updateFilter("occurrence", v)}
						>
							<option value="">{t("opportunities.allFrequencies")}</option>
							<option value="OneTime">{t("opportunities.oneTime")}</option>
							<option value="Recurring">{t("opportunities.recurring")}</option>
						</FilterSelect>

						<div className="flex flex-col gap-1">
							<span className="text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelDateRange")}
							</span>
							<div className="flex items-center gap-2">
								<input
									type="date"
									aria-label={t("opportunities.dateFromLabel")}
									value={dateFrom}
									onChange={(e) => updateFilter("dateFrom", e.target.value)}
									className={dateInputClass(!!dateFrom)}
								/>
								<span className="text-xs text-gray-400" aria-hidden="true">
									-
								</span>
								<input
									type="date"
									aria-label={t("opportunities.dateToLabel")}
									value={dateTo}
									onChange={(e) => updateFilter("dateTo", e.target.value)}
									className={dateInputClass(!!dateTo)}
								/>
							</div>
						</div>
					</div>
				</div>

				{/* Active filter chips */}
				{hasFilters && (
					<div className="flex flex-wrap items-center gap-1.5 border-t border-gray-100 px-4 py-3">
						{search && (
							<FilterChip
								label={search}
								ariaLabel={t("opportunities.clearSearch")}
								onRemove={() => {
									setSearchInput("");
									updateFilter("search", "");
								}}
							/>
						)}
						{city && (
							<FilterChip
								label={city}
								ariaLabel={t("opportunities.clearCity")}
								onRemove={() => {
									setCityInput("");
									updateFilter("city", "");
								}}
							/>
						)}
						{occurrence && (
							<FilterChip
								label={
									occurrence === "OneTime"
										? t("opportunities.oneTime")
										: t("opportunities.recurring")
								}
								ariaLabel={t("opportunities.clearOccurrence")}
								onRemove={() => updateFilter("occurrence", "")}
							/>
						)}
						{participationType && (
							<FilterChip
								label={
									participationType === "Waitlist"
										? t("opportunities.waitlist")
										: t("opportunities.individualContact")
								}
								ariaLabel={t("opportunities.clearType")}
								onRemove={() => updateFilter("participationType", "")}
							/>
						)}
						{isRemoteParam && (
							<FilterChip
								label={
									isRemoteParam === "true"
										? t("opportunities.remote")
										: t("opportunities.onsite")
								}
								ariaLabel={t("opportunities.clearLocation")}
								onRemove={() => updateFilter("isRemote", "")}
							/>
						)}
						{dateFrom && (
							<FilterChip
								label={`${t("opportunities.dateFromLabel")}: ${dateFrom}`}
								ariaLabel={t("opportunities.clearDateFrom")}
								onRemove={() => updateFilter("dateFrom", "")}
							/>
						)}
						{dateTo && (
							<FilterChip
								label={`${t("opportunities.dateToLabel")}: ${dateTo}`}
								ariaLabel={t("opportunities.clearDateTo")}
								onRemove={() => updateFilter("dateTo", "")}
							/>
						)}
						{category && (
							<FilterChip
								label={t(`opportunities.category.${category}`)}
								ariaLabel={t("opportunities.clearCategory")}
								onRemove={() => updateFilter("category", "")}
							/>
						)}
						{tag && (
							<FilterChip
								label={`#${tag}`}
								ariaLabel={t("opportunities.clearTag")}
								onRemove={() => updateFilter("tag", "")}
							/>
						)}
						<button
							type="button"
							onClick={clearFilters}
							className="ml-1 text-xs font-medium text-gray-400 underline hover:text-gray-600"
						>
							{t("opportunities.clearFilters")}
						</button>
					</div>
				)}
			</div>

			{isMap && (
				<OpportunityMap
					items={items}
					bounds={bounds}
					onBoundsChange={handleBoundsChange}
				/>
			)}

			{loading && items.length === 0 && (
				<p className={isMap ? "mt-4 text-gray-500" : "text-gray-500"}>
					{t("opportunities.loading")}
				</p>
			)}
			{error && (
				<p className="text-red-600">
					{t("opportunities.error", { message: error })}
				</p>
			)}

			{!error && (
				<>
					{!loading && items.length === 0 ? (
						isMap ? (
							<p className="mt-4 text-gray-500">{t("map.noPinsInView")}</p>
						) : (
							<EmptyState
								title={t("opportunities.noResults")}
								message={
									search
										? t("opportunities.noResultsWithSearch")
										: hasFilters
											? t("opportunities.noResultsWithFilters")
											: undefined
								}
								action={
									hasFilters
										? {
												label: t("opportunities.clearFilters"),
												onClick: clearFilters,
											}
										: undefined
								}
							/>
						)
					) : (
						<ul className={isMap ? "mt-4 space-y-3" : "space-y-3"}>
							{items.map((item: VolunteerOpportunitySummary) => (
								<li
									key={item.id}
									className="relative rounded border transition-colors hover:bg-gray-50"
								>
									<Link
										to={`/volunteer-opportunities/${item.id}`}
										className="absolute inset-0 rounded"
										aria-label={item.title}
									/>
									<div className="p-4">
										<div className="flex items-start justify-between">
											<div>
												<strong className="block text-sm font-medium">
													{item.title}
												</strong>
												<p className="mt-0.5 text-xs text-gray-500">
													<Link
														to={`/organizations/${item.organizationId}`}
														className="relative z-10 hover:underline"
													>
														{item.organizationName}
													</Link>
												</p>
												<p className="mt-1 text-sm text-gray-600">
													{item.description}
												</p>
											</div>
											<div className="ml-2 flex shrink-0 flex-col items-end gap-1">
												<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
													{formatOccurrence(item.occurrence, t)}
												</span>
												<span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-700">
													{formatParticipationType(item.participationType, t)}
												</span>
												{item.category && (
													<span className="rounded-full bg-green-50 px-2 py-0.5 text-xs text-green-700">
														{t(`opportunities.category.${item.category}`)}
													</span>
												)}
											</div>
										</div>
										{item.tags && item.tags.length > 0 && (
											<div className="relative z-10 mt-2 flex flex-wrap gap-1">
												{item.tags.map((tagItem) => (
													<button
														key={tagItem}
														type="button"
														onClick={() => updateFilter("tag", tagItem)}
														className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 hover:bg-gray-200"
													>
														#{tagItem}
													</button>
												))}
											</div>
										)}
										<div className="mt-2 flex items-center justify-between gap-4 text-xs text-gray-500">
											<span>
												{item.isRemote ? (
													t("opportunities.remote")
												) : (
													<>
														{item.street} {item.houseNumber}, {item.zipCode}{" "}
														{item.city}
													</>
												)}
											</span>
											{item.totalMaxParticipants > 0 &&
												(() => {
													const spotsLeft =
														item.totalMaxParticipants -
														item.currentParticipantCount;
													return spotsLeft <= 0 ? (
														<span className="rounded-full bg-red-100 px-2 py-0.5 font-medium text-red-700">
															{t("opportunities.full")}
														</span>
													) : spotsLeft <= 3 ? (
														<span className="rounded-full bg-orange-100 px-2 py-0.5 font-medium text-orange-700">
															{t("opportunities.spotsLeft", {
																count: spotsLeft,
															})}
														</span>
													) : (
														<span className="rounded-full bg-gray-100 px-2 py-0.5 text-gray-600">
															{t("opportunities.spotsLeft", {
																count: spotsLeft,
															})}
														</span>
													);
												})()}
										</div>
									</div>
								</li>
							))}
						</ul>
					)}

					{!isMap && items.length > 0 && page < pageCount && (
						<div className="mt-4 flex justify-center">
							<button
								onClick={() => setPage((p) => p + 1)}
								disabled={loadingMore}
								className="rounded px-4 py-2 text-sm hover:bg-gray-100 disabled:opacity-40"
							>
								{loadingMore
									? t("opportunities.loading")
									: t("opportunities.loadMore")}
							</button>
						</div>
					)}
				</>
			)}

			{showModal && activeOrgId && (
				<CreateVolunteerOpportunityModal
					organizationId={activeOrgId}
					onClose={() => setShowModal(false)}
					onSuccess={() => setRefreshKey((k) => k + 1)}
				/>
			)}
		</div>
	);
}
