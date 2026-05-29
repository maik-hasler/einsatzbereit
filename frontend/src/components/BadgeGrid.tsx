import { useTranslation } from "react-i18next";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";

const TYPE_ICON: Record<string, string> = {
	Milestone: "🏆",
	Streak: "🔥",
	Social: "🤝",
	Hidden: "✨",
};

const TYPE_NUM_MAP: Record<number, string> = {
	0: "Milestone",
	1: "Streak",
	2: "Social",
	3: "Hidden",
};

function typeLabel(type: string | number): string {
	if (typeof type === "number") return TYPE_NUM_MAP[type] ?? "Milestone";
	return type;
}

interface BadgeCardProps {
	catalog: BadgeCatalogEntry;
	earned?: AchievementSummary;
}

function BadgeCard({ catalog, earned }: BadgeCardProps) {
	const { t, i18n } = useTranslation();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";
	const isEarned = !!earned;
	const isHidden = catalog.isHidden && !isEarned;
	const typeName = isEarned ? typeLabel(earned.type) : typeLabel(catalog.type);
	const icon = TYPE_ICON[typeName] ?? "🏅";
	const tooltipId = `badge-tooltip-${catalog.key}`;

	return (
		<div
			className={`group relative flex flex-col items-center rounded-xl border p-4 text-center transition-all ${
				isEarned
					? "border-brand-200 bg-white shadow-sm hover:shadow-md"
					: "border-gray-100 bg-gray-50"
			}`}
			aria-describedby={!isHidden ? tooltipId : undefined}
		>
			<div
				className={`mb-3 flex h-14 w-14 items-center justify-center rounded-full text-2xl ${
					isEarned ? "bg-brand-50" : "bg-gray-100"
				}`}
			>
				{isHidden ? "?" : icon}
			</div>
			<p
				className={`text-sm font-semibold leading-snug ${
					isEarned ? "text-gray-900" : "text-gray-400"
				}`}
			>
				{isHidden ? t("achievements.lockedBadge") : catalog.name}
			</p>
			{isEarned && (
				<p className="mt-1 text-xs text-gray-500">
					{t("achievements.unlockedOn", {
						date: new Date(earned.unlockedAt).toLocaleDateString(locale),
					})}
				</p>
			)}
			{!isEarned && !isHidden && (
				<p className="mt-1 text-xs text-gray-400">{catalog.description}</p>
			)}
			{!isHidden && (
				<div
					id={tooltipId}
					role="tooltip"
					className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 hidden w-48 -translate-x-1/2 rounded-lg bg-gray-900 px-3 py-2 text-xs text-white shadow-lg group-hover:block"
				>
					<p className="font-semibold">{catalog.name}</p>
					<p className="mt-0.5 text-gray-300">{catalog.description}</p>
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
		return <p className="text-sm text-gray-500">{t("achievements.loading")}</p>;
	}

	const earnedByName = new Map(earned.map((a) => [a.name, a]));

	return (
		<div className="grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6">
			{catalog.map((entry) => (
				<BadgeCard
					key={entry.key}
					catalog={entry}
					earned={earnedByName.get(entry.name)}
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
