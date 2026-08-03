import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { formatDate, formatDateTime, formatOccurrence } from "../../lib/format";
import { getOpportunityCategoryBannerClassName } from "../../lib/opportunityCategoryTheme";
import { useApiClient } from "../../hooks/useApiClient";
import Chip from "../Chip";
import ReportFlagButton from "../ReportFlagButton";
import { CalendarIcon, GlobeIcon, MapPinIcon } from "../icons";
import { CategoryGlyph } from "./CategoryGlyph";

function orgInitials(name: string): string {
	const parts = name.trim().split(/\s+/).filter(Boolean);
	if (parts.length === 0) return "?";
	if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
	return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export default function OpportunityListItem({
	item,
}: {
	item: VolunteerOpportunitySummary;
}) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();
	const isUnlimited = item.totalMaxParticipants == null;
	const spotsLeft =
		item.totalMaxParticipants != null && item.totalMaxParticipants > 0
			? item.totalMaxParticipants - item.currentParticipantCount
			: null;

	return (
		<li className="group relative flex h-full flex-col overflow-hidden rounded-card border border-gray-100 bg-white shadow-resting transition-shadow hover:shadow-raised">
			<Link
				to={`/volunteer-opportunities/${item.id}`}
				className="absolute inset-0 z-10"
				aria-label={item.title}
			/>
			<div className="flex h-full flex-col">
				{/* Banner image or category banner */}
				<div
					className={`relative flex h-32 w-full shrink-0 items-center justify-center overflow-hidden ${getOpportunityCategoryBannerClassName(item.category)}`}
				>
					{item.bannerImageUrl ? (
						<img
							src={item.bannerImageUrl}
							alt=""
							width={1200}
							height={480}
							loading="lazy"
							className="absolute inset-0 h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
						/>
					) : (
						<>
							<div
								aria-hidden="true"
								className="pointer-events-none absolute -top-8 -right-6 h-24 w-24 rounded-full bg-white/15 blur-xl"
							/>
							<div
								aria-hidden="true"
								className="pointer-events-none absolute -bottom-10 -left-6 h-24 w-24 rounded-full bg-black/10 blur-xl"
							/>
							<CategoryGlyph
								category={item.category}
								className="h-11 w-11 text-white/90 transition-transform duration-300 group-hover:scale-110"
							/>
							<span className="absolute right-0 bottom-2 left-0 px-2 text-center text-xs font-semibold tracking-wider text-white/80 uppercase">
								{item.category
									? t(`opportunities.category.${item.category}`)
									: t("opportunities.category.Other")}
							</span>
						</>
					)}
				</div>

				{/* Content */}
				<div className="flex min-w-0 flex-1 flex-col p-4 sm:p-5">
					<div className="mb-2 flex items-center gap-2">
						<Chip tone="neutral" size="sm" className="shrink-0">
							{formatOccurrence(item.occurrence, t)}
						</Chip>
						{isUnlimited ? (
							<Chip tone="brand" size="sm" className="ml-auto shrink-0">
								{t("opportunities.unlimitedSpots")}
							</Chip>
						) : (
							spotsLeft !== null &&
							(spotsLeft <= 0 ? (
								<Chip tone="danger" size="sm" className="ml-auto shrink-0">
									{t("opportunities.full")}
								</Chip>
							) : spotsLeft <= 3 ? (
								<Chip tone="warning" size="sm" className="ml-auto shrink-0">
									{t("opportunities.spotsLeft", {
										count: spotsLeft,
									})}
								</Chip>
							) : (
								<Chip tone="neutral" size="sm" className="ml-auto shrink-0">
									{t("opportunities.spotsLeft", {
										count: spotsLeft,
									})}
								</Chip>
							))
						)}
						{auth.isAuthenticated && (
							<ReportFlagButton
								targetLabel={item.title}
								ariaLabel={t("opportunities.reportOpportunity")}
								onReport={async (reason, details) => {
									await api.reportVolunteerOpportunity(item.id, {
										reason,
										details: details || undefined,
									});
								}}
								className={`relative z-20 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600${spotsLeft === null && !isUnlimited ? "ml-auto" : ""}`}
							/>
						)}
					</div>
					<h3 className="text-base leading-snug font-semibold text-gray-900 transition-colors group-hover:text-brand-700 sm:text-lg">
						{item.title}
					</h3>
					{item.nextTimeSlotStart ? (
						<p className="mt-1 flex items-center gap-1.5 text-sm font-medium text-brand-700">
							<CalendarIcon className="h-4 w-4 shrink-0" />
							<span>
								{formatDateTime(
									item.nextTimeSlotStart as unknown as string,
									i18n.language,
								)}
							</span>
						</p>
					) : item.validUntil ? (
						<p className="mt-1 flex items-center gap-1.5 text-sm font-medium text-gray-500">
							<CalendarIcon className="h-4 w-4 shrink-0" />
							<span>
								{t("opportunities.applyBy", {
									date: formatDate(
										item.validUntil as unknown as string,
										i18n.language,
									),
								})}
							</span>
						</p>
					) : (
						<p className="mt-1 flex items-center gap-1.5 text-sm font-medium text-gray-500">
							<CalendarIcon className="h-4 w-4 shrink-0" />
							<span>{t("opportunities.flexibleDate")}</span>
						</p>
					)}
					{item.description && (
						<p className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-500">
							{item.description}
						</p>
					)}
					<div className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-gray-100 pt-3">
						<Link
							to={`/organizations/${item.organizationId}`}
							className="group/org relative z-20 inline-flex items-center gap-2"
						>
							{item.organizationLogoUrl ? (
								<img
									src={item.organizationLogoUrl}
									alt=""
									width={28}
									height={28}
									loading="lazy"
									className="h-7 w-7 shrink-0 rounded-full object-cover"
								/>
							) : (
								<span
									aria-hidden="true"
									className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-100 text-xs font-bold text-brand-700"
								>
									{orgInitials(item.organizationName)}
								</span>
							)}
							<span className="text-sm font-medium text-gray-600 transition-colors group-hover/org:text-brand-700 group-hover/org:underline">
								{item.organizationName}
							</span>
						</Link>
						{(item.isRemote || item.city) && (
							<span className="ml-auto flex items-center gap-1 text-xs text-gray-500">
								{item.isRemote ? (
									<>
										<GlobeIcon className="h-3.5 w-3.5 shrink-0" />
										<span>{t("opportunities.remote")}</span>
									</>
								) : (
									<>
										<MapPinIcon className="h-3.5 w-3.5 shrink-0" />
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
}
