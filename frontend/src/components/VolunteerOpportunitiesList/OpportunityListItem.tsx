import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { formatDateTime, formatOccurrence } from "../../lib/format";
import { getOpportunityCategoryBannerClassName } from "../../lib/opportunityCategoryTheme";
import { useApiClient } from "../../hooks/useApiClient";
import ReportFlagButton from "../ReportFlagButton";
import { CalendarIcon, CategoryGlyph, GlobeIcon, PinIcon } from "./icons";

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
	const spotsLeft =
		item.totalMaxParticipants > 0
			? item.totalMaxParticipants - item.currentParticipantCount
			: null;

	return (
		<li className="group relative overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-sm transition-all hover:-translate-y-0.5 hover:border-brand-200 hover:shadow-lg">
			<Link
				to={`/volunteer-opportunities/${item.id}`}
				className="absolute inset-0 z-10"
				aria-label={item.title}
			/>
			<div className="flex flex-col sm:flex-row">
				{/* Banner image or category banner */}
				<div
					className={`relative flex h-24 shrink-0 items-center justify-center overflow-hidden sm:h-auto sm:w-36 lg:w-44 ${getOpportunityCategoryBannerClassName(item.category)}`}
				>
					{item.bannerImageUrl ? (
						<img
							src={item.bannerImageUrl}
							alt=""
							className="absolute inset-0 h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
						/>
					) : (
						<>
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
						</>
					)}
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
								className={`relative z-20 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600${spotsLeft === null ? " ml-auto" : ""}`}
							/>
						)}
					</div>
					<h3 className="text-base font-semibold leading-snug text-gray-900 transition-colors group-hover:text-brand-700 sm:text-lg">
						{item.title}
					</h3>
					{item.nextTimeSlotStart && (
						<p className="mt-1 flex items-center gap-1.5 text-sm font-medium text-brand-700">
							<CalendarIcon className="h-4 w-4 shrink-0" />
							<span>
								{formatDateTime(
									item.nextTimeSlotStart as unknown as string,
									i18n.language,
								)}
							</span>
						</p>
					)}
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
							{item.organizationLogoUrl ? (
								<img
									src={item.organizationLogoUrl}
									alt=""
									className="h-7 w-7 shrink-0 rounded-full object-cover"
								/>
							) : (
								<span
									aria-hidden="true"
									className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-100 text-[11px] font-bold text-brand-700"
								>
									{orgInitials(item.organizationName)}
								</span>
							)}
							<span className="text-sm font-medium text-gray-600 transition-colors group-hover/org:text-brand-700 group-hover/org:underline">
								{item.organizationName}
							</span>
							{item.isOrganizationVerified && (
								<svg
									className="h-4 w-4 shrink-0 text-brand-600"
									viewBox="0 0 20 20"
									fill="currentColor"
									aria-label={t("orgProfile.verified")}
									role="img"
								>
									<path
										fillRule="evenodd"
										d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z"
										clipRule="evenodd"
									/>
								</svg>
							)}
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
}
