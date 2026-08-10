import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { formatDate, formatDateTime, formatOccurrence } from "../../lib/format";
import Chip from "../Chip";
import { getInitials } from "../../lib/initials";
import { CalendarIcon, GlobeIcon, MapPinIcon } from "../icons";
import { CategoryGlyph } from "./CategoryGlyph";

// A card carries a banner only when the organization uploaded a photo; there
// is no synthetic per-category color block standing in for one. Both the
// rainbow version (one Tailwind swatch per category) and the single-tint
// glyph tile that replaced it were rejected - the first read as a generic
// admin-dashboard tag system against the muted brand-green language, the
// second put 128px of near-empty rectangle at the top of nearly every card,
// since almost no opportunity has a photo. Cross-checked against
// idealist.org and betterplace.org, neither of which recolors a listing by
// category. Category is carried by the icon+label chip instead.

export default function OpportunityListItem({
	item,
}: {
	item: VolunteerOpportunitySummary;
}) {
	const { t, i18n } = useTranslation();
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
				{/* Banner, only when the organization actually uploaded a photo.
				There used to be a 128px category-glyph tile in its place on every
				photo-less card, added back when the demo content was thin enough
				that cards had little else to tell them apart. With real titles,
				dates, organizations and cities on the card that no longer holds,
				and since almost no opportunity carries a photo it meant most of
				the grid's top third was a tinted rectangle with a small icon
				centred in it. A photo-less card is a text card now. */}
				{item.bannerImageUrl && (
					<div className="relative h-32 w-full shrink-0 overflow-hidden bg-gradient-to-br from-brand-50 to-brand-100">
						<img
							src={item.bannerImageUrl}
							alt=""
							width={1200}
							height={480}
							loading="lazy"
							className="absolute inset-0 h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
						/>
					</div>
				)}

				<div className="flex min-w-0 flex-1 flex-col p-4 sm:p-5">
					<div className="mb-2 flex flex-wrap items-center gap-1.5">
						<Chip tone="brand" size="sm" className="shrink-0">
							<CategoryGlyph category={item.category} className="h-3 w-3" />
							{item.category
								? t(`opportunities.category.${item.category}`)
								: t("opportunities.category.Other")}
						</Chip>
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
						{/* No report control here. It sat inline in this metadata row,
						immediately after the category and capacity chips, giving a
						moderation action the same weight and adjacency as the
						listing's own attributes - and it broke the row's rhythm,
						since it needed a conditional ml-auto depending on whether a
						capacity chip happened to be present. Reporting stays on the
						opportunity's own page, where the reader has the full listing
						in front of them to judge. */}
					</div>
					<h3 className="text-base leading-snug font-semibold text-gray-900 transition-colors group-hover:text-brand-700 sm:text-lg">
						{item.title}
					</h3>
					{/* One slot, one meaning. The start date used to render as a bare
					datetime in brand green while the application deadline rendered
					as "Apply by {date}" in grey - same position, different colour,
					different fact, no label on the first of them. Two cards side by
					side were not comparable. Both are labelled and share a tone
					now; the calendar icon is the only thing they still share
					silently. */}
					{item.nextTimeSlotStart ? (
						<p className="mt-1 flex items-center gap-1.5 text-sm font-medium text-gray-500">
							<CalendarIcon className="h-4 w-4 shrink-0" />
							<span>
								{t("opportunities.startsOn", {
									date: formatDateTime(
										item.nextTimeSlotStart as unknown as string,
										i18n.language,
									),
								})}
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
					{item.tags.length > 0 && (
						<div className="relative z-20 mt-2 flex flex-wrap gap-1.5">
							{item.tags.map((tag) => (
								<Chip
									key={tag}
									tone="neutral"
									size="sm"
									to={`/opportunities?tag=${encodeURIComponent(tag)}`}
									aria-label={t("opportunities.filterByTag", { tag })}
								>
									{tag}
								</Chip>
							))}
						</div>
					)}
					<div className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-gray-100 pt-3">
						<Link
							to={`/organizations/${item.organizationId}`}
							data-testid="opportunity-org-link"
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
									{getInitials(item.organizationName)}
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
