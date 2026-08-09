import { useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import Skeleton from "./Skeleton";
import { formatDate } from "../lib/format";
import { achievementTypeLabel } from "../lib/achievementType";

// Mirrors FilterDropdown.tsx's edge margin - keeps a clamped tooltip from
// touching the viewport edge exactly, not just staying inside it.
const EDGE_MARGIN = 8;
// Matches the tooltip's own w-48 - fixed, so unlike FilterDropdown's
// dynamically-sized panel this doesn't need to be measured (and can't be:
// the tooltip is display:none via the `hidden` class until hovered/focused,
// so getBoundingClientRect/offsetWidth would read 0 anyway).
const TOOLTIP_WIDTH = 192;

function AchievementTypeIcon({
	type,
	className = "h-7 w-7",
}: {
	type: string;
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

	switch (type) {
		case "Milestone":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0"
					/>
				</svg>
			);
		case "Streak":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M15.362 5.214A8.252 8.252 0 0 1 12 21 8.25 8.25 0 0 1 6.038 7.047 8.287 8.287 0 0 0 9 9.601a8.983 8.983 0 0 1 3.361-6.867 8.21 8.21 0 0 0 3 2.48Z"
					/>
				</svg>
			);
		case "Hidden":
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456Z"
					/>
				</svg>
			);
		default:
			return (
				<svg {...svgProps}>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z"
					/>
				</svg>
			);
	}
}

interface BadgeCardProps {
	catalog: BadgeCatalogEntry;
	earned?: AchievementSummary;
}

function BadgeCard({ catalog, earned }: BadgeCardProps) {
	const { t, i18n } = useTranslation();
	const isEarned = !!earned;
	const isHidden = catalog.isHidden && !isEarned;
	const typeName = isEarned
		? achievementTypeLabel(earned.type)
		: achievementTypeLabel(catalog.type);
	const tooltipId = `badge-tooltip-${catalog.key}`;
	const nameId = `badge-name-${catalog.key}`;
	const cardRef = useRef<HTMLDivElement>(null);
	// Centered on the badge by default (see the tooltip's left-1/2 below) -
	// shifted only far enough to keep it on-screen for badges near the edge
	// of the grid's outer columns (#1672). Recomputed on resize since which
	// columns are "outer" depends on the grid's current column count.
	const [tooltipShift, setTooltipShift] = useState(0);

	useLayoutEffect(() => {
		function updatePosition() {
			const card = cardRef.current;
			if (!card) return;
			const cardRect = card.getBoundingClientRect();
			const cardCenter = cardRect.left + cardRect.width / 2;
			const halfTooltipWidth = TOOLTIP_WIDTH / 2;
			const minCenter = EDGE_MARGIN + halfTooltipWidth;
			const maxCenter = window.innerWidth - EDGE_MARGIN - halfTooltipWidth;
			const clampedCenter = Math.min(
				Math.max(cardCenter, minCenter),
				maxCenter,
			);
			setTooltipShift(clampedCenter - cardCenter);
		}
		updatePosition();
		window.addEventListener("resize", updatePosition);
		return () => window.removeEventListener("resize", updatePosition);
	}, []);

	return (
		<div
			ref={cardRef}
			// Locked badges recede to a dashed outline instead of a solid gray-50
			// fill (#1755). With five of six locked, the filled version made the
			// unearned ones the heaviest thing on the profile page and the single
			// earned badge the quietest - exactly backwards. Text colours are
			// unchanged, so the gray-500 label still clears the 4.5:1 floor.
			className={`group relative flex flex-col items-center rounded-card border p-4 text-center transition-all ${
				isEarned
					? "border-brand-200 bg-white shadow-resting hover:shadow-raised"
					: "border-dashed border-gray-200 bg-transparent"
			}`}
			tabIndex={isHidden ? undefined : 0}
			role={isHidden ? undefined : "group"}
			aria-labelledby={!isHidden ? nameId : undefined}
			aria-describedby={!isHidden ? tooltipId : undefined}
		>
			<div
				className={`mb-3 flex h-14 w-14 items-center justify-center rounded-full ${
					isEarned ? "bg-brand-50" : "bg-gray-50"
				}`}
			>
				{isHidden ? (
					<span className="text-2xl" aria-hidden="true">
						?
					</span>
				) : (
					<AchievementTypeIcon
						type={typeName}
						className={`h-7 w-7 ${isEarned ? "text-brand-600" : "text-gray-400"}`}
					/>
				)}
			</div>
			<p
				id={nameId}
				className={`text-sm leading-snug font-semibold ${
					isEarned ? "text-gray-900" : "text-gray-500"
				}`}
			>
				{isHidden
					? t("achievements.lockedBadge")
					: t(`achievements.badges.${catalog.key}.name`, {
							defaultValue: catalog.name,
						})}
			</p>
			{isEarned && (
				<p className="mt-1 text-xs text-gray-500">
					{t("achievements.unlockedOn", {
						date: formatDate(
							earned.unlockedAt as unknown as string,
							i18n.language,
						),
					})}
				</p>
			)}
			{!isEarned && !isHidden && (
				<p className="mt-1 text-xs text-gray-500">
					{t(`achievements.badges.${catalog.key}.description`, {
						defaultValue: catalog.description,
					})}
				</p>
			)}
			{!isHidden && (
				<div
					id={tooltipId}
					role="tooltip"
					style={{ transform: `translateX(calc(-50% + ${tooltipShift}px))` }}
					className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 hidden w-48 rounded-lg bg-gray-900 px-3 py-2 text-xs text-white shadow-lg group-hover:block group-focus:block"
				>
					<p className="font-semibold">
						{t(`achievements.badges.${catalog.key}.name`, {
							defaultValue: catalog.name,
						})}
					</p>
					<p className="mt-0.5 text-gray-300">
						{t(`achievements.badges.${catalog.key}.description`, {
							defaultValue: catalog.description,
						})}
					</p>
					{isEarned && (
						<p className="mt-1 text-brand-300">
							{t("achievements.types." + typeName)}
						</p>
					)}
				</div>
			)}
		</div>
	);
}

interface BadgeGridProps {
	earned: AchievementSummary[];
	catalog: BadgeCatalogEntry[];
	loading?: boolean;
}

export default function BadgeGrid({
	earned,
	catalog,
	loading,
}: BadgeGridProps) {
	const { t } = useTranslation();

	if (loading) {
		return (
			<div role="status" className="grid grid-cols-2 gap-3 sm:grid-cols-3">
				<span className="sr-only">{t("achievements.loading")}</span>
				{Array.from({ length: 6 }).map((_, i) => (
					<Skeleton key={i} className="h-28 w-full" />
				))}
			</div>
		);
	}

	const earnedByKey = new Map(earned.map((a) => [a.key, a]));

	return (
		<div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
			{catalog.map((entry) => (
				<BadgeCard
					key={entry.key}
					catalog={entry}
					earned={earnedByKey.get(entry.key)}
				/>
			))}
			{catalog.length === 0 && earned.length === 0 && (
				<p className="col-span-full text-sm text-gray-500">
					{t("achievements.noAchievements")}
				</p>
			)}
		</div>
	);
}
