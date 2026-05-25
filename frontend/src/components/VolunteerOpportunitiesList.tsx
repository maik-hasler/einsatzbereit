import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence, formatParticipationType } from "../lib/format";
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
			.catch((err: Error) => {
				if (cancelled) return;
				setError(err.message);
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

	return (
		<div>
			<div className="mb-4 flex items-center justify-between">
				<h2 className="text-xl font-semibold">
					{t("opportunities.currentNeeds")}
				</h2>
				<div className="flex items-center gap-2">
					<div
						role="group"
						className="inline-flex overflow-hidden rounded border"
					>
						<button
							type="button"
							data-testid="view-toggle-list"
							onClick={() => setView("list")}
							className={
								!isMap
									? "bg-black px-3 py-1.5 text-sm text-white"
									: "px-3 py-1.5 text-sm hover:bg-gray-100"
							}
						>
							{t("opportunities.view.list")}
						</button>
						<button
							type="button"
							data-testid="view-toggle-map"
							onClick={() => setView("map")}
							className={
								isMap
									? "bg-black px-3 py-1.5 text-sm text-white"
									: "px-3 py-1.5 text-sm hover:bg-gray-100"
							}
						>
							{t("opportunities.view.map")}
						</button>
					</div>
					{canCreateOpportunity && (
						<button
							onClick={() => setShowModal(true)}
							data-testid="create-opportunity-btn"
							className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
						>
							{t("opportunities.createNeed")}
						</button>
					)}
				</div>
			</div>

			<div className="mb-2 sm:hidden">
				<button
					type="button"
					onClick={() => setFiltersOpen((o) => !o)}
					className="flex items-center gap-2 rounded border px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
				>
					<span>{t("opportunities.filterToggle")}</span>
					{activeFilterCount > 0 && (
						<span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-black text-xs text-white">
							{activeFilterCount}
						</span>
					)}
					<svg
						className={`h-4 w-4 text-gray-500 transition-transform ${filtersOpen ? "rotate-180" : ""}`}
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
			</div>

			<div
				className={`mb-4 flex flex-wrap gap-2 ${filtersOpen ? "" : "max-sm:hidden"}`}
			>
				<div className="relative">
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
						className="rounded border px-3 py-1.5 pr-7 text-sm"
					/>
					{searchInput && (
						<button
							type="button"
							onClick={() => {
								setSearchInput("");
								updateFilter("search", "");
							}}
							aria-label={t("opportunities.clearSearch")}
							className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
						>
							&times;
						</button>
					)}
				</div>
				<div className="relative">
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
						className="rounded border px-3 py-1.5 pr-7 text-sm"
					/>
					{cityInput && (
						<button
							type="button"
							onClick={() => {
								setCityInput("");
								updateFilter("city", "");
							}}
							aria-label={t("opportunities.clearCity")}
							className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
						>
							&times;
						</button>
					)}
				</div>
				<select
					aria-label={t("opportunities.allFrequencies")}
					value={occurrence}
					onChange={(e) => updateFilter("occurrence", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allFrequencies")}</option>
					<option value="OneTime">{t("opportunities.oneTime")}</option>
					<option value="Recurring">{t("opportunities.recurring")}</option>
				</select>
				<select
					aria-label={t("opportunities.allTypes")}
					value={participationType}
					onChange={(e) => updateFilter("participationType", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allTypes")}</option>
					<option value="Waitlist">{t("opportunities.waitlist")}</option>
					<option value="IndividualContact">
						{t("opportunities.individualContact")}
					</option>
				</select>
				<select
					aria-label={t("opportunities.allLocations")}
					value={isRemoteParam}
					onChange={(e) => updateFilter("isRemote", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allLocations")}</option>
					<option value="true">{t("opportunities.remote")}</option>
					<option value="false">{t("opportunities.onsite")}</option>
				</select>
				<input
					type="date"
					aria-label={t("opportunities.dateFromLabel")}
					value={dateFrom}
					onChange={(e) => updateFilter("dateFrom", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				/>
				<input
					type="date"
					aria-label={t("opportunities.dateToLabel")}
					value={dateTo}
					onChange={(e) => updateFilter("dateTo", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				/>
				<select
					aria-label={t("opportunities.allCategories")}
					value={category}
					onChange={(e) => updateFilter("category", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
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
				</select>
				{hasFilters && (
					<button
						type="button"
						onClick={clearFilters}
						className="rounded border px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-100"
					>
						{t("opportunities.clearFilters")}
					</button>
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
									hasFilters
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
									className="relative rounded border hover:bg-gray-50 transition-colors"
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
												<p className="mt-1 text-sm text-gray-600">
													{item.description}
												</p>
											</div>
											<div className="flex flex-col items-end gap-1 shrink-0 ml-2">
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
										<div className="relative z-10 mt-2 flex items-center gap-4 text-xs text-gray-500">
											<Link
												to={`/organizations/${item.organizationId}`}
												className="hover:underline"
											>
												{item.organizationName}
											</Link>
											{item.isRemote ? (
												<span>{t("opportunities.remote")}</span>
											) : (
												<span>
													{item.street} {item.houseNumber}, {item.zipCode}{" "}
													{item.city}
												</span>
											)}
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
