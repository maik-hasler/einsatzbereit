import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import {
	formatDate,
	formatDateTime,
	formatOccurrence,
	formatParticipationType,
	isDeadlineImminent,
	pickLocalizedText,
} from "../lib/format";
import Chip, { type ChipTone } from "./Chip";
import { getInitials } from "../lib/initials";
import {
	FEW_SPOTS_THRESHOLD,
	getOpportunityCapacity,
	type OpportunityCapacity,
} from "../lib/opportunityCapacity";
import {
	ArrowsRightLeftIcon,
	CalendarIcon,
	ClockIcon,
	GlobeIcon,
	MapPinIcon,
} from "./icons";
import { CategoryGlyph } from "./VolunteerOpportunitiesList/CategoryGlyph";

// The one opportunity card, shared by every surface that shows one (#2054):
// /opportunities and the landing page preview (both backed by the full
// VolunteerOpportunitySummary), and the organization profile's "current
// needs" list plus the opportunity detail page's "more from this
// organization" rail (both backed by the leaner PublicOpportunitySummaryDto,
// which carries no organization identity, tags or banner - those two blocks
// below are gated on the fields actually being present rather than picking
// between two different card components). Before this, the same opportunity
// rendered as two visually and informationally different cards depending on
// which page it was reached from - most visibly, the org-scoped surfaces
// dropped the date/deadline entirely and showed only "Einmalig"/"Regelmaessig"
// where the other surfaces showed a real date.
export interface OpportunityCardItem {
	id: string;
	titleDe: string;
	titleEn: string | undefined;
	descriptionDe: string | undefined;
	descriptionEn: string | undefined;
	street: string | undefined;
	houseNumber: string | undefined;
	zipCode: string | undefined;
	city: string | undefined;
	isRemote: boolean;
	occurrence: string;
	participationType: string;
	category: string | undefined;
	totalMaxParticipants: number | undefined;
	currentParticipantCount: number;
	validUntil: Date | undefined;
	nextTimeSlotStart: Date | undefined;
	/**
	 * Present only on the richer VolunteerOpportunitySummary DTO - gates the
	 * banner and organization footer below. Absent on PublicOpportunitySummaryDto,
	 * whose surfaces (org profile, "more from this organization") already sit on
	 * that one organization's own page, so a footer repeating its identity would
	 * be redundant; those cards fall back to a plain address line instead.
	 */
	organizationId?: string;
	organizationName?: string;
	organizationLogoUrl?: string;
	tags?: string[];
	bannerImageUrl?: string;
}

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
	item: OpportunityCardItem,
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

export default function OpportunityCard({
	item,
	headingLevel = 2,
}: {
	item: OpportunityCardItem;
	/**
	 * Level for the card's title. Defaults to 2 for a card that is the only
	 * heading above a grid; every current call site sits under its own visible
	 * or sr-only section heading and passes 3.
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
	// Only ever true for the English UI falling back to German content - the
	// German variant is required, so the reverse never happens (#2057). Checked
	// on both fields independently since an organizer may translate one but
	// not the other.
	const isGermanFallback =
		title.lang !== i18n.language ||
		(description !== undefined && description.lang !== i18n.language);
	const hasOrganization = !!item.organizationId && !!item.organizationName;

	// ScheduledSlots is the only participation type that ever carries a real
	// time-slot capacity - IndividualContact opportunities can never have time
	// slots (see VolunteerOpportunity.AddTimeSlot), so their capacity chip
	// below already says "By expression of interest" on its own. A second chip
	// repeating that exact wording here would be the literal duplicate #1943's
	// grid-wording contract ruled out, so this slot only ever states the one
	// fact it can state without repeating the capacity chip.
	const showSignUpMechanismChip = item.participationType === "ScheduledSlots";

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
				aria-label={title.text}
				lang={title.lang}
			/>
			<div className="flex h-full flex-col">
				{/* Banner, only when the organization actually uploaded a photo -
				never present on the leaner PublicOpportunitySummaryDto (see
				OpportunityCardItem's doc comment). A photo-less card is a text
				card, not a tinted placeholder tile. */}
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
						{/* The top-right slot used to be the capacity chip, which for a
						capped/unlimited/not-yet-published opportunity said three
						unrelated kinds of thing in the same spot with no consistent
						colour telling them apart (#2054). It now always states the
						same one fact - how a volunteer signs up - and the capacity
						count moves down next to the date line below. */}
						{showSignUpMechanismChip && (
							<Chip
								data-testid="opportunity-signup-mechanism"
								tone="neutral"
								size="sm"
								className="ml-auto shrink-0"
							>
								{formatParticipationType(item.participationType, t)}
							</Chip>
						)}
					</div>
					{/* h2 by default, h3 when a section heading sits above the grid -
					see the headingLevel prop. */}
					<Heading
						lang={title.lang}
						className="text-base leading-snug font-semibold text-gray-900 underline-offset-2 transition-colors group-hover:text-brand-700 group-hover:underline sm:text-lg"
					>
						{title.text}
					</Heading>
					{isGermanFallback && (
						<p className="mt-0.5 text-xs text-gray-500">
							{t("opportunities.germanOnlyNotice")}
						</p>
					)}
					{/* Date/deadline and capacity, side by side: the two facts that
					used to compete for the top-right slot now sit together instead,
					next to each other rather than one replacing the other. See
					dateLine() above for the three kinds this slot can state. */}
					<div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
						<p
							data-testid="opportunity-date-line"
							data-date-kind={date.kind}
							className={`flex items-center gap-1.5 text-sm font-medium ${date.tone}`}
						>
							<DateIcon className="h-4 w-4 shrink-0" />
							<span>{date.label}</span>
						</p>
						<Chip
							data-testid="opportunity-capacity"
							tone={capacity.tone}
							size="sm"
							className="shrink-0"
						>
							{capacity.label}
						</Chip>
					</div>
					{description && (
						<p
							lang={description.lang}
							className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-500"
						>
							{description.text}
						</p>
					)}
					{item.tags && item.tags.length > 0 && (
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
					{hasOrganization ? (
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
										{getInitials(item.organizationName ?? "")}
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
					) : (
						// No organization footer to repeat: this card already sits on
						// that organization's own page (org profile, or the detail
						// page's "more from this organization" rail), so the full
						// address stands in for it instead of a city-only badge.
						<div className="mt-auto flex items-start gap-2 pt-2 text-sm text-gray-600">
							{item.isRemote ? (
								<>
									<GlobeIcon className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
									<span>{t("opportunities.remote")}</span>
								</>
							) : (
								item.street && (
									<>
										<MapPinIcon className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
										<span>
											{item.street} {item.houseNumber}, {item.zipCode}{" "}
											{item.city}
										</span>
									</>
								)
							)}
						</div>
					)}
				</div>
			</div>
		</li>
	);
}
