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

interface NominatimPlace {
	address?: {
		city?: string;
		town?: string;
		village?: string;
		municipality?: string;
	};
}

function SearchIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
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
				d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
			/>
		</svg>
	);
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

function CalendarIcon({ className = "h-3.5 w-3.5" }: { className?: string }) {
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
				d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"
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
			<span className="shrink-0 text-brand-500">{icon}</span>
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

function FilterSelect({
	icon,
	fieldLabel,
	ariaLabel,
	value,
	onChange,
	children,
}: {
	icon?: React.ReactNode;
	fieldLabel: string;
	ariaLabel: string;
	value: string;
	onChange: (v: string) => void;
	children: React.ReactNode;
}) {
	const active = !!value;
	return (
		<div className="flex shrink-0 flex-col gap-1">
			<span className="flex items-center gap-1 text-xs font-medium text-gray-500">
				{icon && (
					<span className="text-gray-400" aria-hidden="true">
						{icon}
					</span>
				)}
				{fieldLabel}
			</span>
			<div className="relative">
				{icon && (
					<span
						className={`pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 ${active ? "text-brand-500" : "text-gray-400"}`}
						aria-hidden="true"
					>
						{icon}
					</span>
				)}
				<select
					aria-label={ariaLabel}
					value={value}
					onChange={(e) => onChange(e.target.value)}
					className={`appearance-none cursor-pointer rounded-lg border py-2 ${icon ? "pl-8" : "pl-3"} pr-8 text-sm font-medium transition-colors focus:outline-none ${
						active
							? "border-brand-500 bg-brand-50 text-brand-700"
							: "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
					}`}
				>
					{children}
				</select>
				<ChevronIcon
					className={`pointer-events-none absolute right-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 ${active ? "text-brand-500" : "text-gray-400"}`}
				/>
			</div>
		</div>
	);
}

function ToggleGroup({
	icon,
	fieldLabel,
	ariaGroupLabel,
	options,
	value,
	onChange,
}: {
	icon?: React.ReactNode;
	fieldLabel: string;
	ariaGroupLabel: string;
	options: { value: string; label: string }[];
	value: string;
	onChange: (v: string) => void;
}) {
	return (
		<div className="flex shrink-0 flex-col gap-1">
			<span className="flex items-center gap-1 text-xs font-medium text-gray-500">
				{icon && (
					<span className="text-gray-400" aria-hidden="true">
						{icon}
					</span>
				)}
				{fieldLabel}
			</span>
			<div
				role="group"
				aria-label={ariaGroupLabel}
				className="inline-flex overflow-hidden rounded-lg border border-gray-200"
			>
				{options.map((opt, i) => (
					<button
						key={opt.value}
						type="button"
						aria-pressed={value === opt.value}
						onClick={() => onChange(opt.value)}
						className={`whitespace-nowrap px-3 py-2 text-sm transition-colors ${
							i > 0 ? "border-l border-gray-200" : ""
						} ${
							value === opt.value
								? "bg-brand-50 font-medium text-brand-700"
								: "bg-white text-gray-600 hover:bg-gray-50"
						}`}
					>
						{opt.label}
					</button>
				))}
			</div>
		</div>
	);
}

function DateRangePicker({
	dateFrom,
	dateTo,
	onChangeDateFrom,
	onChangeDateTo,
	labels,
}: {
	dateFrom: string;
	dateTo: string;
	onChangeDateFrom: (v: string) => void;
	onChangeDateTo: (v: string) => void;
	icon?: React.ReactNode;
	labels: {
		fieldLabel: string;
		anyDate: string;
		from: string;
		to: string;
		clearDates: string;
		dateFromAriaLabel: string;
		dateToAriaLabel: string;
	};
}) {
	const [open, setOpen] = useState(false);
	const wrapperRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		function handleOutside(e: MouseEvent) {
			if (
				wrapperRef.current &&
				!wrapperRef.current.contains(e.target as Node)
			) {
				setOpen(false);
			}
		}
		document.addEventListener("mousedown", handleOutside);
		return () => document.removeEventListener("mousedown", handleOutside);
	}, []);

	const hasDate = !!(dateFrom || dateTo);
	const triggerText = !hasDate
		? labels.anyDate
		: dateFrom && dateTo
			? `${dateFrom} - ${dateTo}`
			: dateFrom
				? `${labels.from} ${dateFrom}`
				: `${labels.to} ${dateTo}`;

	return (
		<div ref={wrapperRef} className="relative flex shrink-0 flex-col gap-1">
			<span className="flex items-center gap-1 text-xs font-medium text-gray-500">
				{icon && (
					<span className="text-gray-400" aria-hidden="true">
						{icon}
					</span>
				)}
				{labels.fieldLabel}
			</span>
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				className={`flex items-center gap-2 rounded-lg border py-2 pl-3 pr-2.5 text-sm font-medium transition-colors focus:outline-none ${
					hasDate
						? "border-brand-500 bg-brand-50 text-brand-700"
						: "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
				}`}
			>
				<CalendarIcon
					className={`h-4 w-4 shrink-0 ${hasDate ? "text-brand-500" : "text-gray-400"}`}
				/>
				<span>{triggerText}</span>
				<ChevronIcon
					className={`h-3.5 w-3.5 ${hasDate ? "text-brand-500" : "text-gray-400"}`}
					open={open}
				/>
			</button>

			{open && (
				<div className="absolute top-full z-20 mt-1.5 w-64 rounded-xl border border-gray-200 bg-white shadow-lg">
					<div className="flex flex-col gap-4 p-4">
						<div>
							<div className="mb-1.5 flex items-center justify-between">
								<span className="text-xs font-medium text-gray-500">
									{labels.from}
								</span>
								{dateFrom && (
									<button
										type="button"
										onClick={() => onChangeDateFrom("")}
										className="text-xs text-gray-400 hover:text-gray-600"
										aria-label={labels.dateFromAriaLabel}
									>
										&times;
									</button>
								)}
							</div>
							<input
								type="date"
								aria-label={labels.dateFromAriaLabel}
								value={dateFrom}
								max={dateTo || undefined}
								onChange={(e) => onChangeDateFrom(e.target.value)}
								className={`w-full rounded-lg border px-3 py-2 text-sm focus:outline-none ${
									dateFrom
										? "border-brand-500 bg-brand-50 text-brand-700"
										: "border-gray-200 bg-white text-gray-700 focus:border-brand-500"
								}`}
							/>
						</div>
						<div>
							<div className="mb-1.5 flex items-center justify-between">
								<span className="text-xs font-medium text-gray-500">
									{labels.to}
								</span>
								{dateTo && (
									<button
										type="button"
										onClick={() => onChangeDateTo("")}
										className="text-xs text-gray-400 hover:text-gray-600"
										aria-label={labels.dateToAriaLabel}
									>
										&times;
									</button>
								)}
							</div>
							<input
								type="date"
								aria-label={labels.dateToAriaLabel}
								value={dateTo}
								min={dateFrom || undefined}
								onChange={(e) => onChangeDateTo(e.target.value)}
								className={`w-full rounded-lg border px-3 py-2 text-sm focus:outline-none ${
									dateTo
										? "border-brand-500 bg-brand-50 text-brand-700"
										: "border-gray-200 bg-white text-gray-700 focus:border-brand-500"
								}`}
							/>
						</div>
					</div>
					{hasDate && (
						<div className="border-t border-gray-100 px-4 py-3">
							<button
								type="button"
								onClick={() => {
									onChangeDateFrom("");
									onChangeDateTo("");
									setOpen(false);
								}}
								className="w-full rounded-lg bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-200"
							>
								{labels.clearDates}
							</button>
						</div>
					)}
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
	const [citySuggestions, setCitySuggestions] = useState<string[]>([]);
	const [showSuggestions, setShowSuggestions] = useState(false);

	const searchDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
	const cityDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
	const nominatimAbortRef = useRef<AbortController | null>(null);
	const cityWrapperRef = useRef<HTMLDivElement>(null);

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

	useEffect(() => {
		if (cityInput.length < 2) {
			setCitySuggestions([]);
			setShowSuggestions(false);
			return;
		}
		nominatimAbortRef.current?.abort();
		const controller = new AbortController();
		nominatimAbortRef.current = controller;
		const timer = setTimeout(async () => {
			try {
				const res = await fetch(
					`https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&featuretype=city&q=${encodeURIComponent(cityInput)}&limit=6`,
					{
						signal: controller.signal,
						headers: { "Accept-Language": "de,en" },
					},
				);
				if (!res.ok) return;
				const data = (await res.json()) as NominatimPlace[];
				const cities = data
					.map(
						(r) =>
							r.address?.city ??
							r.address?.town ??
							r.address?.village ??
							r.address?.municipality ??
							"",
					)
					.filter((c) => c.length > 0)
					.filter((c, i, arr) => arr.indexOf(c) === i)
					.slice(0, 6);
				setCitySuggestions(cities);
				setShowSuggestions(cities.length > 0);
			} catch {
				// AbortError on cleanup or network error - ignore
			}
		}, 350);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
	}, [cityInput]);

	useEffect(() => {
		function handleOutside(e: MouseEvent) {
			if (
				cityWrapperRef.current &&
				!cityWrapperRef.current.contains(e.target as Node)
			) {
				setShowSuggestions(false);
			}
		}
		document.addEventListener("mousedown", handleOutside);
		return () => document.removeEventListener("mousedown", handleOutside);
	}, []);

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

	function selectCitySuggestion(suggestion: string) {
		setCityInput(suggestion);
		updateFilter("city", suggestion);
		setShowSuggestions(false);
	}

	const occurrenceOptions = [
		{ value: "", label: t("opportunities.all") },
		{ value: "OneTime", label: t("opportunities.oneTime") },
		{ value: "Recurring", label: t("opportunities.recurring") },
	];

	const typeOptions = [
		{ value: "", label: t("opportunities.all") },
		{ value: "Waitlist", label: t("opportunities.waitlist") },
		{ value: "IndividualContact", label: t("opportunities.individualContact") },
	];

	const locationOptions = [
		{ value: "", label: t("opportunities.all") },
		{ value: "true", label: t("opportunities.remote") },
		{ value: "false", label: t("opportunities.onsite") },
	];

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
				{/* Always-visible: search + city */}
				<div className="p-4 pb-3">
					<div className="flex flex-col gap-3 sm:flex-row">
						<div className="flex flex-1 flex-col gap-1">
							<span className="text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelSearch")}
							</span>
							<div className="relative">
								<SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
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

						<div ref={cityWrapperRef} className="flex flex-col gap-1 sm:w-52">
							<span className="text-xs font-medium text-gray-500">
								{t("opportunities.filterLabelCity")}
							</span>
							<div className="relative">
								<PinIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
								<input
									type="text"
									role="combobox"
									aria-autocomplete="list"
									aria-expanded={showSuggestions}
									aria-haspopup="listbox"
									aria-controls="city-suggestions"
									aria-label={t("opportunities.cityPlaceholder")}
									placeholder={t("opportunities.cityPlaceholder")}
									value={cityInput}
									onFocus={() => {
										if (citySuggestions.length > 0) setShowSuggestions(true);
									}}
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
											setCitySuggestions([]);
											setShowSuggestions(false);
										}}
										aria-label={t("opportunities.clearCity")}
										className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
									>
										&times;
									</button>
								)}
								{showSuggestions && (
									<ul
										id="city-suggestions"
										role="listbox"
										className="absolute top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg"
									>
										{citySuggestions.map((s, i) => (
											<li
												key={i}
												role="option"
												aria-selected={false}
												tabIndex={-1}
												onMouseDown={(e) => e.preventDefault()}
												onClick={() => selectCitySuggestion(s)}
												onKeyDown={(e) => {
													if (e.key === "Enter" || e.key === " ")
														selectCitySuggestion(s);
												}}
												className="cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700"
											>
												<span className="flex items-center gap-2">
													<PinIcon className="h-3.5 w-3.5 shrink-0 text-gray-400" />
													{s}
												</span>
											</li>
										))}
									</ul>
								)}
							</div>
						</div>
					</div>
				</div>

				{/* Secondary filter row - always visible */}
				<div className="border-t border-gray-100">
					<div className="flex flex-wrap items-end gap-3 px-4 pb-4 pt-3">
						<FilterSelect
							icon={<TagIcon className="h-3.5 w-3.5" />}
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

						<ToggleGroup
							icon={<UsersIcon className="h-3.5 w-3.5" />}
							fieldLabel={t("opportunities.filterLabelType")}
							ariaGroupLabel={t("opportunities.allTypes")}
							options={typeOptions}
							value={participationType}
							onChange={(v) => updateFilter("participationType", v)}
						/>

						<ToggleGroup
							icon={<GlobeIcon className="h-3.5 w-3.5" />}
							fieldLabel={t("opportunities.filterLabelLocation")}
							ariaGroupLabel={t("opportunities.allLocations")}
							options={locationOptions}
							value={isRemoteParam}
							onChange={(v) => updateFilter("isRemote", v)}
						/>

						<ToggleGroup
							icon={<ClockIcon className="h-3.5 w-3.5" />}
							fieldLabel={t("opportunities.filterLabelFrequency")}
							ariaGroupLabel={t("opportunities.allFrequencies")}
							options={occurrenceOptions}
							value={occurrence}
							onChange={(v) => updateFilter("occurrence", v)}
						/>

						<DateRangePicker
							icon={<CalendarIcon className="h-3.5 w-3.5" />}
							dateFrom={dateFrom}
							dateTo={dateTo}
							onChangeDateFrom={(v) => updateFilter("dateFrom", v)}
							onChangeDateTo={(v) => updateFilter("dateTo", v)}
							labels={{
								fieldLabel: t("opportunities.filterLabelDateRange"),
								anyDate: t("opportunities.anyDate"),
								from: t("opportunities.dateFromLabel"),
								to: t("opportunities.dateToLabel"),
								clearDates: t("opportunities.clearDateRange"),
								dateFromAriaLabel: t("opportunities.dateFromLabel"),
								dateToAriaLabel: t("opportunities.dateToLabel"),
							}}
						/>
					</div>
				</div>

				{/* Active filter chips */}
				{hasFilters && (
					<div className="flex flex-wrap items-center gap-1.5 border-t border-gray-100 px-4 py-3">
						{search && (
							<FilterChip
								icon={<SearchIcon />}
								chipLabel={t("opportunities.filterLabelSearch")}
								value={search}
								ariaLabel={t("opportunities.clearSearch")}
								onRemove={() => {
									setSearchInput("");
									updateFilter("search", "");
								}}
							/>
						)}
						{city && (
							<FilterChip
								icon={<PinIcon />}
								chipLabel={t("opportunities.filterLabelCity")}
								value={city}
								ariaLabel={t("opportunities.clearCity")}
								onRemove={() => {
									setCityInput("");
									updateFilter("city", "");
								}}
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
						{dateFrom && (
							<FilterChip
								icon={<CalendarIcon />}
								chipLabel={t("opportunities.dateFromLabel")}
								value={dateFrom}
								ariaLabel={t("opportunities.clearDateFrom")}
								onRemove={() => updateFilter("dateFrom", "")}
							/>
						)}
						{dateTo && (
							<FilterChip
								icon={<CalendarIcon />}
								chipLabel={t("opportunities.dateToLabel")}
								value={dateTo}
								ariaLabel={t("opportunities.clearDateTo")}
								onRemove={() => updateFilter("dateTo", "")}
							/>
						)}
						{category && (
							<FilterChip
								icon={<TagIcon />}
								chipLabel={t("opportunities.filterLabelCategory")}
								value={t(`opportunities.category.${category}`)}
								ariaLabel={t("opportunities.clearCategory")}
								onRemove={() => updateFilter("category", "")}
							/>
						)}
						{tag && (
							<FilterChip
								icon={<HashIcon />}
								chipLabel={t("opportunities.filterLabelTag")}
								value={tag}
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
