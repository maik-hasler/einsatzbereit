import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import {
	formatDate,
	formatDateTime,
	formatOccurrence,
	isDeadlineImminent,
	pickLocalizedText,
} from "../../lib/format";
import Chip, { type ChipTone } from "../Chip";
import { getInitials } from "../../lib/initials";
import {
	FEW_SPOTS_THRESHOLD,
	getOpportunityCapacity,
	type OpportunityCapacity,
} from "../../lib/opportunityCapacity";
import {
	ArrowsRightLeftIcon,
	CalendarIcon,
	ClockIcon,
	GlobeIcon,
	MapPinIcon,
} from "../icons";
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

/**
 * The capacity chip always renders, in every one of the contract's states -
 * including the "no places to count" one, which used to render nothing at all
 * and made the chip's presence look like a property of the opportunity rather
 * than of the data (#1777).
 */
export function capacityChip(
	capacity: OpportunityCapacity,
	t: TFunction,
): { tone: ChipTone; label: string } {
	switch (capacity.kind) {
		case "unlimited":
			return { tone: "brand", label: t("opportunities.unlimitedSpots") };
		case "notApplicable":
			return {
				tone: "neutral",
				label:
					capacity.reason === "interest"
						? t("opportunities.byInterest")
						: t("opportunities.noSpotsYet"),
			};
		case "capped":
			if (capacity.isFull) {
				return { tone: "danger", label: t("opportunities.full") };
			}
			return {
				tone: capacity.spotsLeft <= FEW_SPOTS_THRESHOLD ? "warning" : "neutral",
				label: t("opportunities.spotsLeft", { count: capacity.spotsLeft }),
			};
	}
}

/**
 * The date line's three kinds, each with its own glyph and tone.
 *
 * They used to share one icon and one tone, differing only in their label
 * text, on the reasoning that a shared tone made two cards comparable - and
 * it did fix the older bug where a bare unlabelled datetime in brand green sat
 * in the same slot as a grey "Express interest by" line. But identical styling traded one
 * failure for another: a start date and an application deadline are not the
 * same kind of fact, and reading which one a card states required reading the
 * label on every card in the grid. Keeping both labels *and* giving each kind
 * its own glyph and tone (calendar/neutral for a date that is set,
 * clock/amber for a deadline running down, arrows/muted for no fixed date)
 * makes the kind legible at a glance without going back to an unlabelled
 * slot. Deliberate reversal of the previous decision, per #1777.
 *
 * The deadline's amber only applies within isDeadlineImminent's window - a
 * deadline months out used the same warning tone as one closing tomorrow,
 * which drowned out the actually-urgent ones (#2088). Outside that window it
 * falls back to the same neutral tone as a set start date.
 */
function dateLine(
	item: VolunteerOpportunitySummary,
	t: TFunction,
	language: string,
): {
	kind: "start" | "deadline" | "flexible";
	Icon: typeof CalendarIcon;
	tone: string;
	label: string;
} {
	if (item.nextTimeSlotStart) {
		return {
			kind: "start",
			Icon: CalendarIcon,
			tone: "text-gray-700",
			label: t("opportunities.startsOn", {
				date: formatDateTime(
					item.nextTimeSlotStart as unknown as string,
					language,
				),
			}),
		};
	}

	if (item.validUntil) {
		const validUntil = item.validUntil as unknown as string;
		return {
			kind: "deadline",
			Icon: ClockIcon,
			tone: isDeadlineImminent(validUntil) ? "text-amber-700" : "text-gray-700",
			label: t("opportunities.applyBy", {
				date: formatDate(validUntil, language),
			}),
		};
	}

	return {
		kind: "flexible",
		Icon: ArrowsRightLeftIcon,
		tone: "text-gray-500",
		label: t("opportunities.flexibleDate"),
	};
}

export default function OpportunityListItem({
	item,
	headingLevel = 2,
}: {
	item: VolunteerOpportunitySummary;
	/**
	 * Level for the card's title. Defaults to 2 for /opportunities, where the
	 * page header band's h1 is the only heading above the grid. The landing
	 * page's preview passes 3: its own section heading is an h2, and cards
	 * belong under it rather than beside it.
	 */
	headingLevel?: 2 | 3;
}) {
	const { t, i18n } = useTranslation();
	const Heading = headingLevel === 3 ? "h3" : "h2";
	const capacity = capacityChip(getOpportunityCapacity(item), t);
	const date = dateLine(item, t, i18n.language);
	const DateIcon = date.Icon;
	const title = pickLocalizedText(item.titleDe, item.titleEn, i18n.language);
	const description = pickLocalizedText(
		item.descriptionDe,
		item.descriptionEn,
		i18n.language,
	);

	// No overflow-hidden on the card any more. The stretched link below is what
	// a keyboard user actually lands on (the title is inside it, not focusable
	// itself), and global.css's shared :focus-visible ring draws at
	// outline-offset 2px - i.e. entirely outside the link's box, which is the
	// card's box, so clipping descendants clipped the whole ring away and
	// tabbing through the grid moved an invisible focus (#1777). The banner
	// below carries the top rounding itself now, which is all the clipping was
	// for.
	return (
		<li className="group relative flex h-full flex-col rounded-card border border-gray-100 bg-white shadow-resting transition-shadow hover:shadow-raised">
			<Link
				to={`/volunteer-opportunities/${item.id}`}
				className="absolute inset-0 z-10 rounded-card"
				aria-label={title}
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
					<div className="relative h-32 w-full shrink-0 overflow-hidden rounded-t-card bg-gradient-to-br from-brand-50 to-brand-100">
						<img
							src={item.bannerImageUrl}
							alt=""
							width={1200}
							height={480}
							loading="lazy"
							className="absolute inset-0 h-full w-full object-cover transition-transform duration-300 group-hover:scale-105 motion-reduce:transition-none"
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
						<Chip
							data-testid="opportunity-capacity"
							tone={capacity.tone}
							size="sm"
							className="ml-auto shrink-0"
						>
							{capacity.label}
						</Chip>
						{/* No report control here. It sat inline in this metadata row,
						immediately after the category and capacity chips, giving a
						moderation action the same weight and adjacency as the
						listing's own attributes - and it broke the row's rhythm,
						since it needed a conditional ml-auto depending on whether a
						capacity chip happened to be present. Reporting stays on the
						opportunity's own page, where the reader has the full listing
						in front of them to judge. */}
					</div>
					{/* h2 by default, h3 when a section heading sits above the grid -
					see the headingLevel prop. This list used to be fixed at h3
					because it lived inside the landing page behind a "Current
					Opportunities" <h2>; #1755 gave it its own route, where the page
					header band's <h1> is the only heading above it, so a fixed h3
					skipped a level and axe failed the page on heading-order. The
					landing page has a section heading over these cards again, hence
					a prop rather than a second fixed level. */}
					<Heading className="text-base leading-snug font-semibold text-gray-900 underline-offset-2 transition-colors group-hover:text-brand-700 group-hover:underline sm:text-lg">
						{title}
					</Heading>
					{/* See dateLine() above for the three kinds this slot can state and
					why each one carries its own glyph and tone. */}
					<p
						data-testid="opportunity-date-line"
						data-date-kind={date.kind}
						className={`mt-1 flex items-center gap-1.5 text-sm font-medium ${date.tone}`}
					>
						<DateIcon className="h-4 w-4 shrink-0" />
						<span>{date.label}</span>
					</p>
					{description && (
						<p className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-500">
							{description}
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
