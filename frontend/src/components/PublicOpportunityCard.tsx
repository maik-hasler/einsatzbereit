import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOpportunitySummaryDto } from "../client/api-client";
import { formatOccurrence, pickLocalizedText } from "../lib/format";
import { getOpportunityCapacity } from "../lib/opportunityCapacity";
import Chip from "./Chip";
import { CalendarIcon, GlobeIcon, MapPinIcon } from "./icons";
import { capacityChip } from "./VolunteerOpportunitiesList/OpportunityListItem";
import { CategoryGlyph } from "./VolunteerOpportunitiesList/CategoryGlyph";

// The one card for a PublicOpportunitySummaryDto, shared by the organization
// profile's "current needs" list and the opportunity detail page's "more from
// this organization" teaser (#1755). Both hand-rolled their own version
// before: a title, a clamped description and a row of three neutral chips, so
// the two most important facts - when it runs and where - were the same weight
// as everything else and the card had no anchor for the eye.
//
// Still NOT VolunteerOpportunitiesList/OpportunityListItem, which is the
// richer card the landing page grid uses: that one needs a banner image and
// organization identity to render its full layout, and neither exists on
// this DTO. The category chip and capacity badge below reuse OpportunityListItem's
// own `capacityChip` and the shared `getOpportunityCapacity` resolver rather
// than re-deriving the mapping, so the two cards state a given opportunity's
// standing identically (#1912) - this DTO gained the backing fields for
// exactly that reason.
//
// The rest reuses the label/value language of the detail page's at-a-glance
// panel: the date leads in brand-700 with its icon, location follows muted.
// No separate participation-type chip below - for an interest-based
// opportunity that repeated the capacity badge's own "By expression of
// interest" wording verbatim (#1943's grid-wording contract caught the
// duplicate), and for a slot-based one the capacity badge already carries
// the more specific spots-left/full state.
export default function PublicOpportunityCard({
	opportunity,
}: {
	opportunity: PublicOpportunitySummaryDto;
}) {
	const { t, i18n } = useTranslation();
	const capacity = capacityChip(getOpportunityCapacity(opportunity), t);
	const title = pickLocalizedText(
		opportunity.titleDe,
		opportunity.titleEn,
		i18n.language,
	);
	const description = pickLocalizedText(
		opportunity.descriptionDe,
		opportunity.descriptionEn,
		i18n.language,
	);

	return (
		<li className="group relative flex h-full flex-col rounded-card border border-gray-100 bg-white p-5 shadow-resting transition-shadow hover:shadow-raised">
			<Link
				to={`/volunteer-opportunities/${opportunity.id}`}
				className="absolute inset-0 rounded-card"
				aria-label={title}
			/>

			{/* Category left, capacity top-right - the same chip row
			OpportunityListItem renders, so this opportunity reads the same way
			here as it does on the public grid and its own detail page (#1912). */}
			<div className="mb-2 flex flex-wrap items-center gap-1.5">
				<Chip tone="brand" size="sm" className="shrink-0">
					<CategoryGlyph category={opportunity.category} className="h-3 w-3" />
					{opportunity.category
						? t(`opportunities.category.${opportunity.category}`)
						: t("opportunities.category.Other")}
				</Chip>
				<Chip
					data-testid="opportunity-capacity"
					tone={capacity.tone}
					size="sm"
					className="ml-auto shrink-0"
				>
					{capacity.label}
				</Chip>
			</div>

			{/* Muted, matching OpportunityListItem's date line. This led in
				brand-700 while the list card's equivalent slot was grey, so the
				same fact was styled as the card's loudest element in one place and
				as supporting metadata in the other - most visible on the
				opportunity detail page, which shows the list card's siblings above
				and these below. */}
			<p className="flex items-center gap-2 text-sm font-medium text-gray-500">
				<CalendarIcon className="h-4 w-4 shrink-0" />
				{formatOccurrence(opportunity.occurrence, t)}
			</p>

			<h3 className="mt-2 font-display text-xl font-bold text-gray-900 group-hover:text-brand-800">
				{title}
			</h3>

			{description && (
				<p className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-600">
					{description}
				</p>
			)}

			<div className="mt-3 flex items-start gap-2 text-sm text-gray-600">
				{opportunity.isRemote ? (
					<>
						<GlobeIcon className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
						<span>{t("opportunities.remote")}</span>
					</>
				) : (
					opportunity.street && (
						<>
							<MapPinIcon className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
							<span>
								{opportunity.street} {opportunity.houseNumber},{" "}
								{opportunity.zipCode} {opportunity.city}
							</span>
						</>
					)
				)}
			</div>
		</li>
	);
}
