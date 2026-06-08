import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence } from "../lib/format";
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

function fmtIso(d: Date): string {
	return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function fmtShortDate(iso: string): string {
	const d = new Date(iso + "T00:00:00");
	return new Intl.DateTimeFormat(undefined, {
		day: "numeric",
		month: "short",
	}).format(d);
}

function orgInitials(name: string): string {
	const parts = name.trim().split(/\s+/).filter(Boolean);
	if (parts.length === 0) return "?";
	if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
	return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

// ── Icons ────────────────────────────────────────────────────────────────────

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

function ChevronLeftIcon() {
	return (
		<svg
			className="h-4 w-4"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.75 19.5 8.25 12l7.5-7.5"
			/>
		</svg>
	);
}

function ChevronRightIcon() {
	return (
		<svg
			className="h-4 w-4"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m8.25 4.5 7.5 7.5-7.5 7.5"
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

function BroomIcon() {
	return (
		<svg
			className="h-3.5 w-3.5"
			viewBox="0 0 24 24"
			fill="none"
			stroke="currentColor"
			strokeWidth="2"
			strokeLinecap="round"
			strokeLinejoin="round"
			aria-hidden="true"
		>
			<path d="m13 11 9-9" />
			<path d="M14.6 12.6c.8.8.9 2.1.2 3L10 22l-8-8 6.4-4.8c.9-.7 2.2-.6 3 .2Z" />
			<path d="m6.8 10.4 6.8 6.8" />
			<path d="m5 17 1.4-1.4" />
		</svg>
	);
}

function ViewListIcon() {
	return (
		<svg
			className="h-4 w-4"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3.75 5.25h16.5M3.75 12h16.5m-16.5 6.75h16.5"
			/>
		</svg>
	);
}

function ViewMapIcon() {
	return (
		<svg
			className="h-4 w-4"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9 6.75V15m6-6v8.25m-8.25 0h10.5a2.25 2.25 0 0 0 2.25-2.25V8.25a2.25 2.25 0 0 0-2.25-2.25H6.75A2.25 2.25 0 0 0 4.5 8.25v5.25a2.25 2.25 0 0 0 2.25 2.25Z"
			/>
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

// ── Category glyph (decorative banner icon) ──────────────────────────────────

function CategoryGlyph({
	category,
	className = "h-10 w-10",
}: {
	category: string | undefined;
	className?: string;
}) {
	const svgProps = {
		className,
		fill: "none" as const,
		viewBox: "0 0 24 24",
		strokeWidth: 1.5,
		stroke: "currentColor",
		"aria-hidden": true,
	};

	switch (category) {
		case "Social":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z"
					/>
				</svg>
			);
		case "Environment":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
					/>
				</svg>
			);
		case "Sport":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0"
					/>
				</svg>
			);
		case "Education":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5"
					/>
				</svg>
			);
		case "DisasterRelief":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9 12.75 11.25 15 15 9.75m-3-7.036A11.959 11.959 0 0 1 3.598 6 11.99 11.99 0 0 0 3 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285Z"
					/>
				</svg>
			);
		case "Health":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z"
					/>
				</svg>
			);
		case "Animals":
			return (
				<svg
					className={className}
					viewBox="0 0 24 24"
					fill="currentColor"
					aria-hidden="true"
				>
					<ellipse cx="12" cy="16" rx="4" ry="3.2" />
					<circle cx="6.5" cy="11" r="1.9" />
					<circle cx="17.5" cy="11" r="1.9" />
					<circle cx="9" cy="7" r="1.9" />
					<circle cx="15" cy="7" r="1.9" />
				</svg>
			);
		case "Culture":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9 9l10.5-3m0 6.553v3.75a2.25 2.25 0 0 1-1.632 2.163l-1.32.377a1.803 1.803 0 1 1-.99-3.467l2.31-.66a2.25 2.25 0 0 0 1.632-2.163zm0 0V2.25L9 5.25v10.303m0 0v3.75a2.25 2.25 0 0 1-1.632 2.163l-1.32.377a1.803 1.803 0 0 1-.99-3.467l2.31-.66A2.25 2.25 0 0 0 9 15.553z"
					/>
				</svg>
			);
		case "Technology":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25"
					/>
				</svg>
			);
		default:
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456Z"
					/>
				</svg>
			);
	}
}

// ── MiniCalendar ─────────────────────────────────────────────────────────────

function MiniCalendar({
	fromStr,
	toStr,
	onChange,
}: {
	fromStr: string;
	toStr: string;
	onChange: (from: string, to: string) => void;
}) {
	const { t } = useTranslation();

	const todayMidnight = (() => {
		const d = new Date();
		d.setHours(0, 0, 0, 0);
		return d;
	})();

	const parseIso = (s: string): Date | null => {
		if (!s) return null;
		const d = new Date(s + "T00:00:00");
		return isNaN(d.getTime()) ? null : d;
	};

	const from = parseIso(fromStr);
	const to = parseIso(toStr);

	const [calYear, setCalYear] = useState<number>(() =>
		(from ?? todayMidnight).getFullYear(),
	);
	const [calMonth, setCalMonth] = useState<number>(() =>
		(from ?? todayMidnight).getMonth(),
	);
	const [hover, setHover] = useState<Date | null>(null);

	const firstOfMonth = new Date(calYear, calMonth, 1);
	const daysInMonth = new Date(calYear, calMonth + 1, 0).getDate();
	const startDow = (firstOfMonth.getDay() + 6) % 7; // Mon=0

	const cells: (Date | null)[] = [
		...Array<null>(startDow).fill(null),
		...Array.from(
			{ length: daysInMonth },
			(_, i) => new Date(calYear, calMonth, i + 1),
		),
	];
	while (cells.length % 7 !== 0) cells.push(null);

	const effTo = from && !to && hover && hover >= from ? hover : to;
	const rangeA = from && effTo ? (from <= effTo ? from : effTo) : null;
	const rangeB = from && effTo ? (from <= effTo ? effTo : from) : null;

	function clickDay(day: Date) {
		if (!from || (from && to)) {
			onChange(fmtIso(day), "");
		} else if (day < from) {
			onChange(fmtIso(day), "");
		} else if (day.getTime() === from.getTime()) {
			onChange("", "");
		} else {
			onChange(fromStr, fmtIso(day));
		}
	}

	function prevMonth() {
		if (calMonth === 0) {
			setCalMonth(11);
			setCalYear((y) => y - 1);
		} else {
			setCalMonth((m) => m - 1);
		}
	}
	function nextMonth() {
		if (calMonth === 11) {
			setCalMonth(0);
			setCalYear((y) => y + 1);
		} else {
			setCalMonth((m) => m + 1);
		}
	}

	const monthName = new Intl.DateTimeFormat(undefined, {
		month: "long",
	}).format(firstOfMonth);
	const dayLabels = Array.from({ length: 7 }, (_, i) => {
		const ref = new Date(2024, 0, 1 + i); // 2024-01-01 was Monday
		return new Intl.DateTimeFormat(undefined, { weekday: "short" }).format(ref);
	});

	return (
		<div className="w-64 select-none p-3">
			<div className="mb-2 flex items-center justify-between">
				<button
					type="button"
					onClick={prevMonth}
					aria-label={t("opportunities.prevMonth")}
					className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
				>
					<ChevronLeftIcon />
				</button>
				<span className="text-sm font-medium text-gray-800">
					{monthName} {calYear}
				</span>
				<button
					type="button"
					onClick={nextMonth}
					aria-label={t("opportunities.nextMonth")}
					className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
				>
					<ChevronRightIcon />
				</button>
			</div>

			<div className="mb-1 grid grid-cols-7">
				{dayLabels.map((dl, i) => (
					<div
						key={i}
						className="py-1 text-center text-[11px] font-medium text-gray-400"
					>
						{dl}
					</div>
				))}
			</div>

			<div className="grid grid-cols-7">
				{cells.map((day, i) => {
					if (!day) return <div key={i} className="h-9" />;

					const t0 = day.getTime();
					const isToday = t0 === todayMidnight.getTime();
					const isFrom = from !== null && t0 === from.getTime();
					const isTo = to !== null && t0 === to.getTime();
					const isEdge = isFrom || isTo;
					const inRange =
						rangeA !== null && rangeB !== null && day > rangeA && day < rangeB;
					const isRangeStart =
						rangeA !== null && rangeB !== null && t0 === rangeA.getTime();
					const isRangeEnd =
						rangeA !== null && rangeB !== null && t0 === rangeB.getTime();
					const isHoverRange =
						from !== null &&
						!to &&
						hover !== null &&
						hover >= from &&
						day > from &&
						day <= hover;

					return (
						<div
							key={i}
							className={[
								"flex h-9 items-center justify-center",
								inRange || isHoverRange ? "bg-brand-100" : "",
								isRangeStart && rangeB ? "rounded-l-full" : "",
								isRangeEnd && rangeA ? "rounded-r-full" : "",
							]
								.join(" ")
								.trim()}
						>
							<button
								type="button"
								onClick={() => clickDay(day)}
								onMouseEnter={() => {
									if (from && !to) setHover(day);
								}}
								onMouseLeave={() => {
									if (from && !to) setHover(null);
								}}
								className={[
									"flex h-8 w-8 items-center justify-center rounded-full text-sm transition-colors",
									isEdge
										? "bg-brand-600 font-semibold text-white"
										: isToday
											? "font-medium text-brand-700 ring-2 ring-brand-300 hover:bg-brand-50"
											: "text-gray-700 hover:bg-gray-100",
								].join(" ")}
							>
								{day.getDate()}
							</button>
						</div>
					);
				})}
			</div>

			{from && (
				<div className="mt-2 flex items-center justify-between border-t border-gray-100 pt-2">
					<span className="text-xs text-gray-500">
						{fmtShortDate(fmtIso(from))}
						{to ? ` - ${fmtShortDate(fmtIso(to))}` : ""}
					</span>
					<button
						type="button"
						onClick={() => onChange("", "")}
						className="text-xs text-gray-400 hover:text-gray-600"
					>
						{t("opportunities.clearDate")}
					</button>
				</div>
			)}
		</div>
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
			className={`flex w-full items-center gap-2 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "font-medium text-brand-700" : "text-gray-700"
			}`}
		>
			{selected && (
				<svg
					className="h-4 w-4 shrink-0 text-brand-600"
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
	const containerRef = useRef<HTMLDivElement>(null);
	const [alignRight, setAlignRight] = useState(false);

	useEffect(() => {
		if (isOpen && containerRef.current) {
			const rect = containerRef.current.getBoundingClientRect();
			setAlignRight(rect.left + 300 > window.innerWidth - 8);
		}
	}, [isOpen]);

	return (
		<div ref={containerRef} className="relative shrink-0">
			<div
				role="group"
				aria-label={label}
				className={`inline-flex items-stretch overflow-hidden rounded-full border transition-all ${
					active
						? "border-brand-500 bg-brand-50"
						: "border-gray-200 bg-white hover:border-brand-300 hover:bg-brand-50/50"
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
						className={`shrink-0 ${active ? "text-brand-500" : "text-brand-400"}`}
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
				<div
					className={`absolute ${alignRight ? "right-0" : "left-0"} top-full z-20 mt-1.5 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-xl`}
				>
					{children}
				</div>
			)}
		</div>
	);
}

// ── Main component ────────────────────────────────────────────────────────────

export default function VolunteerOpportunitiesList({
	canCreateOpportunity,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
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
	const [locationSuggestions, setLocationSuggestions] = useState<
		CitySuggestion[]
	>([]);
	const [showLocationSuggestions, setShowLocationSuggestions] = useState(false);

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
				// AbortError or network - ignore
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
		dateFrom,
		dateTo,
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
			prev.dateFrom !== dateFrom ||
			prev.dateTo !== dateTo ||
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
			dateFrom,
			dateTo,
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
		const dateFromParsed = dateFrom ? new Date(dateFrom) : undefined;
		const dateToParsed = dateTo ? new Date(dateTo) : undefined;

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
				dateFromParsed,
				dateToParsed,
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
		dateFrom,
		dateTo,
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

	function handleBoundsChange(next: OpportunityBounds) {
		setBounds(next);
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
					from: fmtShortDate(dateFrom),
					to: fmtShortDate(dateTo),
				})
			: t("opportunities.dateFromDisplay", { date: fmtShortDate(dateFrom) })
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
				{canCreateOpportunity && (
					<button
						type="button"
						onClick={() => setShowModal(true)}
						data-testid="create-opportunity-btn"
						className="mt-5 inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-700 focus:outline-none"
					>
						{t("opportunities.createNeed")}
					</button>
				)}
			</div>

			{/* Filter bar - view toggle + filters in one unified row */}
			<div ref={filterBarRef} className="mb-2">
				<div className="flex flex-wrap items-center justify-center gap-2 pb-3">
					{/* View toggle */}
					<div className="inline-flex items-center overflow-hidden rounded-full border border-gray-200 bg-white text-sm font-medium">
						<button
							type="button"
							data-testid="view-toggle-list"
							aria-pressed={!isMap}
							onClick={() => setView("list")}
							className={`flex items-center gap-1.5 px-3 py-1.5 transition-all ${
								!isMap
									? "bg-brand-50 text-brand-700"
									: "text-gray-600 hover:bg-gray-50"
							}`}
						>
							<ViewListIcon />
							{t("opportunities.view.list")}
						</button>
						<button
							type="button"
							data-testid="view-toggle-map"
							aria-pressed={isMap}
							onClick={() => setView("map")}
							className={`flex items-center gap-1.5 px-3 py-1.5 transition-all ${
								isMap
									? "bg-brand-50 text-brand-700"
									: "text-gray-600 hover:bg-gray-50"
							}`}
						>
							<ViewMapIcon />
							{t("opportunities.view.map")}
						</button>
					</div>
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
								<PinIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
								<input
									type="text"
									aria-label={t("opportunities.filterLabelCity")}
									placeholder={t("opportunities.locationPlaceholder")}
									value={locationCityInput}
									onChange={(e) => setLocationCityInput(e.target.value)}
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
							className="flex items-center gap-1.5 rounded-full border border-red-200 bg-red-50 px-3 py-1.5 text-sm font-medium text-red-600 transition-colors hover:border-red-300 hover:bg-red-100"
						>
							<BroomIcon />
							{t("opportunities.clearFilters")}
						</button>
					)}
				</div>
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
							{items.map((item: VolunteerOpportunitySummary) => {
								const spotsLeft =
									item.totalMaxParticipants > 0
										? item.totalMaxParticipants - item.currentParticipantCount
										: null;
								return (
									<li
										key={item.id}
										className="group relative overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-sm transition-all hover:-translate-y-0.5 hover:border-brand-200 hover:shadow-lg"
									>
										<Link
											to={`/volunteer-opportunities/${item.id}`}
											className="absolute inset-0 z-10"
											aria-label={item.title}
										/>
										<div className="flex flex-col sm:flex-row">
											{/* Category banner */}
											<div className="relative flex h-24 shrink-0 items-center justify-center overflow-hidden bg-gradient-to-br from-brand-500 to-brand-800 sm:h-auto sm:w-36 lg:w-44">
												<div
													aria-hidden="true"
													className="pointer-events-none absolute -right-6 -top-8 h-24 w-24 rounded-full bg-white/15 blur-xl"
												/>
												<div
													aria-hidden="true"
													className="pointer-events-none absolute -bottom-10 -left-6 h-24 w-24 rounded-full bg-black/10 blur-xl"
												/>
												<CategoryGlyph
													category={item.category}
													className="h-11 w-11 text-white/90 transition-transform duration-300 group-hover:scale-110"
												/>
												<span className="absolute bottom-2 left-0 right-0 px-2 text-center text-[10px] font-semibold uppercase tracking-wider text-white/80">
													{item.category
														? t(`opportunities.category.${item.category}`)
														: t("opportunities.category.Other")}
												</span>
											</div>

											{/* Content */}
											<div className="min-w-0 flex-1 p-4 sm:p-5">
												<div className="mb-2 flex items-center gap-2">
													<span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">
														{formatOccurrence(item.occurrence, t)}
													</span>
													{spotsLeft !== null &&
														(spotsLeft <= 0 ? (
															<span className="ml-auto shrink-0 rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-600">
																{t("opportunities.full")}
															</span>
														) : spotsLeft <= 3 ? (
															<span className="ml-auto shrink-0 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">
																{t("opportunities.spotsLeft", {
																	count: spotsLeft,
																})}
															</span>
														) : (
															<span className="ml-auto shrink-0 rounded-full bg-gray-50 px-2 py-0.5 text-xs text-gray-500">
																{t("opportunities.spotsLeft", {
																	count: spotsLeft,
																})}
															</span>
														))}
												</div>
												<h3 className="text-base font-semibold leading-snug text-gray-900 transition-colors group-hover:text-brand-700 sm:text-lg">
													{item.title}
												</h3>
												{item.description && (
													<p className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-500">
														{item.description}
													</p>
												)}
												<div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-gray-100 pt-3">
													<Link
														to={`/organizations/${item.organizationId}`}
														className="group/org relative z-20 inline-flex items-center gap-2"
													>
														<span
															aria-hidden="true"
															className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-100 text-[11px] font-bold text-brand-700"
														>
															{orgInitials(item.organizationName)}
														</span>
														<span className="text-sm font-medium text-gray-600 transition-colors group-hover/org:text-brand-700 group-hover/org:underline">
															{item.organizationName}
														</span>
													</Link>
													{(item.isRemote || item.city) && (
														<span className="ml-auto flex items-center gap-1 text-xs text-gray-400">
															{item.isRemote ? (
																<>
																	<GlobeIcon className="h-3.5 w-3.5 shrink-0" />
																	<span>{t("opportunities.remote")}</span>
																</>
															) : (
																<>
																	<PinIcon className="h-3.5 w-3.5 shrink-0" />
																	<span>{item.city}</span>
																</>
															)}
														</span>
													)}
												</div>
											</div>
										</div>
									</li>
								);
							})}
						</ul>
					)}

					{!isMap && items.length > 0 && page < pageCount && (
						<div className="mt-8 flex justify-center">
							<button
								onClick={() => setPage((p) => p + 1)}
								disabled={loadingMore}
								className="rounded-xl border border-brand-200 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
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
