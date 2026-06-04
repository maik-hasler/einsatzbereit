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

interface NominatimPlace {
	lat: string;
	lon: string;
	address?: {
		city?: string;
		town?: string;
		village?: string;
		municipality?: string;
	};
}

interface CitySuggestion {
	label: string;
	lat: number;
	lng: number;
}

function PinIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
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
	);
}

function TagIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9.568 3H5.25A2.25 2.25 0 0 0 3 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 0 0 5.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 0 0 9.568 3Z"
			/>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 6h.008v.008H6V6Z"
			/>
		</svg>
	);
}

function UsersIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z"
			/>
		</svg>
	);
}

function ClockIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
			/>
		</svg>
	);
}

function GlobeIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
			/>
		</svg>
	);
}

function HashIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M5.25 8.25h15m-16.5 7.5h15m-1.8-13.5-3.9 19.5m-2.1-19.5-3.9 19.5"
			/>
		</svg>
	);
}

function ChevronIcon({
	className = "h-3.5 w-3.5",
	open = false,
}: {
	className?: string;
	open?: boolean;
}) {
	return (
		<svg
			className={`${className} transition-transform ${open ? "rotate-180" : ""}`}
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
	);
}

function ChipXIcon() {
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

function CheckMiniIcon() {
	return (
		<svg
			className="h-2.5 w-2.5 text-white"
			viewBox="0 0 10 10"
			fill="none"
			stroke="currentColor"
			strokeWidth="1.5"
			strokeLinecap="round"
			strokeLinejoin="round"
			aria-hidden="true"
		>
			<path d="M1.5 5.5l2.5 2.5 4.5-5" />
		</svg>
	);
}

function FilterChip({
	icon,
	chipLabel,
	value,
	ariaLabel,
	onRemove,
}: {
	icon: React.ReactNode;
	chipLabel: string;
	value: string;
	ariaLabel: string;
	onRemove: () => void;
}) {
	return (
		<span className="inline-flex items-center gap-1.5 rounded-full bg-brand-100 py-1 pl-2.5 pr-1 text-xs text-brand-800">
			<span className="shrink-0 text-brand-500" aria-hidden="true">
				{icon}
			</span>
			<span>
				<span className="text-brand-600">{chipLabel}:</span>{" "}
				<strong className="font-semibold">{value}</strong>
			</span>
			<button
				type="button"
				onClick={onRemove}
				aria-label={ariaLabel}
				className="ml-0.5 rounded-full p-0.5 hover:bg-brand-200"
			>
				<ChipXIcon />
			</button>
		</span>
	);
}

function DropdownOption({
	label,
	selected,
	onClick,
}: {
	label: string;
	selected: boolean;
	onClick: () => void;
}) {
	return (
		<button
			type="button"
			onClick={onClick}
			className={`flex w-full items-center gap-2.5 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "font-medium text-brand-700" : "text-gray-700"
			}`}
		>
			<span className="h-4 w-4 shrink-0">
				{selected && (
					<svg
						className="h-4 w-4 text-brand-600"
						viewBox="0 0 20 20"
						fill="currentColor"
						aria-hidden="true"
					>
						<path
							fillRule="evenodd"
							d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
							clipRule="evenodd"
						/>
					</svg>
				)}
			</span>
			{label}
		</button>
	);
}

function MultiDropdownOption({
	label,
	selected,
	onClick,
}: {
	label: string;
	selected: boolean;
	onClick: () => void;
}) {
	return (
		<button
			type="button"
			onClick={onClick}
			aria-pressed={selected}
			className={`flex w-full items-center gap-2.5 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "text-brand-700" : "text-gray-700"
			}`}
		>
			<span
				className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors ${
					selected
						? "border-brand-500 bg-brand-500"
						: "border-gray-300 bg-white"
				}`}
			>
				{selected && <CheckMiniIcon />}
			</span>
			{label}
		</button>
	);
}

function FilterDropdown({
	testId,
	icon,
	label,
	displayValue,
	isOpen,
	onToggle,
	onClear,
	clearAriaLabel,
	children,
}: {
	testId?: string;
	icon: React.ReactNode;
	label: string;
	displayValue: string;
	isOpen: boolean;
	onToggle: () => void;
	onClear: () => void;
	clearAriaLabel: string;
	children: React.ReactNode;
}) {
	const active = !!displayValue;
	return (
		<div className="relative shrink-0">
			<div
				role="group"
				aria-label={label}
				className={`inline-flex items-stretch overflow-hidden rounded-full border transition-all ${
					active
						? "border-brand-500 bg-brand-50"
						: "border-gray-200 bg-white hover:border-gray-300"
				}`}
			>
				<button
					type="button"
					data-testid={testId}
					onClick={onToggle}
					aria-expanded={isOpen}
					className={`flex items-center gap-1.5 whitespace-nowrap py-1.5 text-sm transition-colors ${
						active
							? "pl-3 pr-1.5 font-medium text-brand-700"
							: "px-3 text-gray-600 hover:bg-gray-50"
					}`}
				>
					<span
						className={`shrink-0 ${active ? "text-brand-500" : "text-gray-400"}`}
						aria-hidden="true"
					>
						{icon}
					</span>
					<span>{active ? displayValue : label}</span>
					{!active && (
						<ChevronIcon
							className={`h-3 w-3 text-gray-400 transition-transform ${isOpen ? "rotate-180" : ""}`}
						/>
					)}
				</button>
				{active && (
					<button
						type="button"
						onClick={onClear}
						aria-label={clearAriaLabel}
						className="flex items-center px-2 py-1.5 text-brand-400 transition-colors hover:bg-brand-100 hover:text-brand-600"
					>
						<ChipXIcon />
					</button>
				)}
			</div>
			{isOpen && (
				<div className="absolute left-0 top-full z-20 mt-1.5 min-w-[10rem] overflow-hidden rounded-xl border border-gray-200 bg-white shadow-xl">
					{children}
				</div>
			)}
		</div>
	);
}

export default function VolunteerOpportunitiesList({
	canCreateOpportunity,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [searchParams, setSearchParams] = useSearchParams();

	const occurrence = searchParams.get("occurrence") ?? "";
	const participationType = searchParams.get("participationType") ?? "";
	const isRemoteParam = searchParams.get("isRemote") ?? "";
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
	const [locationSuggestions, setLocationSuggestions] = useState<
		CitySuggestion[]
	>([]);
	const [showLocationSuggestions, setShowLocationSuggestions] = useState(false);
	const [locationLocating, setLocationLocating] = useState(false);
	const [locationError, setLocationError] = useState<string | null>(null);

	const nominatimAbortRef = useRef<AbortController | null>(null);
	const filterBarRef = useRef<HTMLDivElement>(null);

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

	useEffect(() => {
		if (locationCityInput.length < 2) {
			setLocationSuggestions([]);
			setShowLocationSuggestions(false);
			return;
		}
		nominatimAbortRef.current?.abort();
		const controller = new AbortController();
		nominatimAbortRef.current = controller;
		const timer = setTimeout(async () => {
			try {
				const res = await fetch(
					`https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&featuretype=city&q=${encodeURIComponent(locationCityInput)}&limit=6`,
					{
						signal: controller.signal,
						headers: { "Accept-Language": "de,en" },
					},
				);
				if (!res.ok) return;
				const data = (await res.json()) as NominatimPlace[];
				const suggestions: CitySuggestion[] = data
					.map((r) => ({
						label:
							r.address?.city ??
							r.address?.town ??
							r.address?.village ??
							r.address?.municipality ??
							"",
						lat: parseFloat(r.lat),
						lng: parseFloat(r.lon),
					}))
					.filter((s) => s.label.length > 0)
					.filter(
						(s, i, arr) => arr.findIndex((x) => x.label === s.label) === i,
					)
					.slice(0, 6);
				setLocationSuggestions(suggestions);
				setShowLocationSuggestions(suggestions.length > 0);
			} catch {
				// AbortError or network error - ignore
			}
		}, 350);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
	}, [locationCityInput]);

	const prevFiltersRef = useRef({
		lat,
		lng,
		radius,
		occurrence,
		participationType,
		isRemoteParam,
		categories: categoriesParam,
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
			prev.lat !== lat ||
			prev.lng !== lng ||
			prev.radius !== radius ||
			prev.occurrence !== occurrence ||
			prev.participationType !== participationType ||
			prev.isRemoteParam !== isRemoteParam ||
			prev.categories !== categoriesParam ||
			prev.tag !== tag ||
			prev.isMap !== isMap ||
			prev.bn !== bounds?.north ||
			prev.bs !== bounds?.south ||
			prev.be !== bounds?.east ||
			prev.bw !== bounds?.west ||
			prev.refreshKey !== refreshKey;

		prevFiltersRef.current = {
			lat,
			lng,
			radius,
			occurrence,
			participationType,
			isRemoteParam,
			categories: categoriesParam,
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

		const centerLatitude = hasLocation ? parseFloat(lat) : undefined;
		const centerLongitude = hasLocation ? parseFloat(lng) : undefined;
		const radiusKm = hasLocation ? parseFloat(radius) : undefined;

		api
			.getVolunteerOpportunities(
				page,
				isMap ? MAP_PAGE_SIZE : LIST_PAGE_SIZE,
				undefined,
				occurrence || undefined,
				participationType || undefined,
				isRemoteBool,
				mapBounds?.north,
				mapBounds?.south,
				mapBounds?.east,
				mapBounds?.west,
				centerLatitude,
				centerLongitude,
				radiusKm,
				selectedCategories.length > 0 ? selectedCategories : undefined,
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
		lat,
		lng,
		radius,
		occurrence,
		participationType,
		isRemoteParam,
		categoriesParam,
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
				next.delete("city");
				next.delete("lat");
				next.delete("lng");
				next.delete("radius");
				next.delete("occurrence");
				next.delete("participationType");
				next.delete("isRemote");
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

	function handleUseMyLocation() {
		if (!navigator.geolocation) {
			setLocationError(t("opportunities.locationNotSupported"));
			return;
		}
		setLocationLocating(true);
		setLocationError(null);
		navigator.geolocation.getCurrentPosition(
			(pos) => {
				setLocationLocating(false);
				const currentRadius = searchParams.get("radius") || "10";
				const params = new URLSearchParams(window.location.search);
				params.delete("city");
				params.set("lat", pos.coords.latitude.toString());
				params.set("lng", pos.coords.longitude.toString());
				params.set("radius", currentRadius);
				setSearchParams(params, { replace: true });
				setLocationCityInput("");
				setOpenFilter(null);
			},
			() => {
				setLocationLocating(false);
				setLocationError(t("opportunities.locationError"));
			},
		);
	}

	function handleBoundsChange(next: OpportunityBounds) {
		setBounds(next);
	}

	const hasFilters = !!(
		hasLocation ||
		occurrence ||
		participationType ||
		isRemoteParam ||
		selectedCategories.length > 0 ||
		tag
	);

	const locationDisplayValue = hasLocation
		? city
			? `${city} · ${radius} km`
			: `${t("opportunities.myLocation")} · ${radius} km`
		: "";

	const categoryDisplayValue =
		selectedCategories.length === 0
			? ""
			: selectedCategories.length === 1
				? t(`opportunities.category.${selectedCategories[0]}`)
				: t("opportunities.nCategoriesSelected", {
						count: selectedCategories.length,
					});

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
									: "border-l border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-brand-50 hover:text-brand-700"
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
				{/* Filter pills */}
				<div
					ref={filterBarRef}
					className="flex flex-wrap items-center gap-2 p-4"
				>
					{/* Location + Radius */}
					<FilterDropdown
						icon={<PinIcon className="h-3.5 w-3.5" />}
						label={t("opportunities.filterLabelLocation")}
						displayValue={locationDisplayValue}
						isOpen={openFilter === "location"}
						onToggle={() => {
							if (openFilter !== "location") {
								setLocationCityInput(city);
								setLocationSuggestions([]);
								setShowLocationSuggestions(false);
								setLocationError(null);
							}
							setOpenFilter((f) => (f === "location" ? null : "location"));
						}}
						onClear={clearLocation}
						clearAriaLabel={t("opportunities.clearLocation")}
					>
						<div className="w-72 p-4">
							{/* City input */}
							<p className="mb-1.5 text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelCity")}
							</p>
							<div className="relative mb-3">
								<PinIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
								<input
									type="text"
									aria-label={t("opportunities.filterLabelCity")}
									placeholder={t("opportunities.locationPlaceholder")}
									value={locationCityInput}
									onChange={(e) => {
										setLocationCityInput(e.target.value);
									}}
									onBlur={() =>
										setTimeout(() => setShowLocationSuggestions(false), 150)
									}
									onFocus={() => {
										if (locationSuggestions.length > 0)
											setShowLocationSuggestions(true);
									}}
									className="w-full rounded-lg border border-gray-200 bg-gray-50 py-2 pl-9 pr-8 text-sm text-gray-900 placeholder:text-gray-400 focus:border-brand-500 focus:bg-white focus:outline-none"
								/>
								{locationCityInput && (
									<button
										type="button"
										onClick={() => {
											setLocationCityInput("");
											setLocationSuggestions([]);
											setShowLocationSuggestions(false);
										}}
										aria-label={t("opportunities.clearCity")}
										className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
									>
										&times;
									</button>
								)}
								{showLocationSuggestions && (
									<ul
										role="listbox"
										className="absolute top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg"
									>
										{locationSuggestions.map((s, i) => (
											<li
												key={i}
												role="option"
												aria-selected={false}
												tabIndex={-1}
												onMouseDown={(e) => e.preventDefault()}
												onClick={() => selectLocationSuggestion(s)}
												onKeyDown={(e) => {
													if (e.key === "Enter" || e.key === " ")
														selectLocationSuggestion(s);
												}}
												className="cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700"
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

							{/* Or divider */}
							<div className="mb-3 flex items-center gap-2 text-xs text-gray-400">
								<div className="flex-1 border-t border-gray-100" />
								{t("opportunities.orLabel")}
								<div className="flex-1 border-t border-gray-100" />
							</div>

							{/* GPS button */}
							<button
								type="button"
								onClick={handleUseMyLocation}
								disabled={locationLocating}
								className="mb-3 flex w-full items-center justify-center gap-2 rounded-lg border border-gray-200 py-2 text-sm text-gray-600 hover:bg-gray-50 disabled:opacity-50"
							>
								<PinIcon className="h-4 w-4" />
								{locationLocating
									? t("opportunities.locating")
									: t("opportunities.useMyLocation")}
							</button>

							{locationError && (
								<p className="mb-3 text-xs text-red-500">{locationError}</p>
							)}

							{/* Radius selector */}
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
							participationType === "Waitlist"
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
							selected={participationType === "Waitlist"}
							onClick={() => {
								updateFilter("participationType", "Waitlist");
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
						label={t("opportunities.filterLabelLocation")}
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

					{/* Tag (static pill) */}
					{tag && (
						<div
							role="group"
							aria-label={`${t("opportunities.filterLabelTag")}: ${tag}`}
							className="inline-flex items-stretch overflow-hidden rounded-full border border-brand-500 bg-brand-50"
						>
							<span className="flex items-center gap-1.5 whitespace-nowrap py-1.5 pl-3 pr-1.5 text-sm font-medium text-brand-700">
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
							className="ml-auto text-xs font-medium text-gray-400 underline hover:text-gray-600"
						>
							{t("opportunities.clearFilters")}
						</button>
					)}
				</div>

				{/* Active filter chips */}
				{hasFilters && (
					<div className="flex flex-wrap items-center gap-1.5 border-t border-gray-100 px-4 py-2.5">
						{hasLocation && (
							<FilterChip
								icon={<PinIcon />}
								chipLabel={t("opportunities.filterLabelLocation")}
								value={locationDisplayValue}
								ariaLabel={t("opportunities.clearLocation")}
								onRemove={clearLocation}
							/>
						)}
						{selectedCategories.length > 0 && (
							<FilterChip
								icon={<TagIcon />}
								chipLabel={t("opportunities.filterLabelCategory")}
								value={categoryDisplayValue}
								ariaLabel={t("opportunities.clearCategory")}
								onRemove={() => {
									const params = new URLSearchParams(window.location.search);
									params.delete("categories");
									setSearchParams(params, { replace: true });
								}}
							/>
						)}
						{participationType && (
							<FilterChip
								icon={<UsersIcon />}
								chipLabel={t("opportunities.filterLabelType")}
								value={
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
								icon={<GlobeIcon />}
								chipLabel={t("opportunities.filterLabelLocation")}
								value={
									isRemoteParam === "true"
										? t("opportunities.remote")
										: t("opportunities.onsite")
								}
								ariaLabel={t("opportunities.clearLocation")}
								onRemove={() => updateFilter("isRemote", "")}
							/>
						)}
						{occurrence && (
							<FilterChip
								icon={<ClockIcon />}
								chipLabel={t("opportunities.filterLabelFrequency")}
								value={
									occurrence === "OneTime"
										? t("opportunities.oneTime")
										: t("opportunities.recurring")
								}
								ariaLabel={t("opportunities.clearOccurrence")}
								onRemove={() => updateFilter("occurrence", "")}
							/>
						)}
						{tag && (
							<FilterChip
								icon={<HashIcon />}
								chipLabel={t("opportunities.filterLabelTag")}
								value={`#${tag}`}
								ariaLabel={t("opportunities.clearTag")}
								onRemove={() => updateFilter("tag", "")}
							/>
						)}
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
