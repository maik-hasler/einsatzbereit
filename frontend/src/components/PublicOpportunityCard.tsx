import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOpportunitySummaryDto } from "../client/api-client";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import Chip from "./Chip";
import { CalendarIcon, GlobeIcon, MapPinIcon } from "./icons";

// The one card for a PublicOpportunitySummaryDto, shared by the organization
// profile's "current needs" list and the opportunity detail page's "more from
// this organization" teaser (#1755). Both hand-rolled their own version
// before: a title, a clamped description and a row of three neutral chips, so
// the two most important facts - when it runs and where - were the same weight
// as everything else and the card had no anchor for the eye.
//
// Deliberately NOT VolunteerOpportunitiesList/OpportunityListItem, which is the
// richer card the landing page grid uses: that one needs category, participant
// counts and a banner image to render its glyph tile and spots-left line, and
// none of those exist on this DTO. Matching its markup here would mean faking
// data the public endpoint does not return.
//
// Instead this reuses the label/value language of the detail page's at-a-glance
// panel: the date leads in brand-700 with its icon, location follows muted, and
// the participation type is the single chip - so a card and the page it links
// to describe an opportunity the same way.
export default function PublicOpportunityCard({
	opportunity,
}: {
	opportunity: PublicOpportunitySummaryDto;
}) {
	const { t } = useTranslation();

	return (
		<li className="group relative flex h-full flex-col rounded-card border border-gray-100 bg-white p-5 shadow-resting transition-shadow hover:shadow-raised">
			<Link
				to={`/volunteer-opportunities/${opportunity.id}`}
				className="absolute inset-0 rounded-card"
				aria-label={opportunity.title}
			/>

			<p className="flex items-center gap-2 text-sm font-semibold text-brand-700">
				<CalendarIcon className="h-4 w-4 shrink-0" />
				{formatOccurrence(opportunity.occurrence, t)}
			</p>

			<h3 className="mt-2 font-display text-xl font-bold text-gray-900 group-hover:text-brand-800">
				{opportunity.title}
			</h3>

			{opportunity.description && (
				<p className="mt-1 line-clamp-2 text-sm leading-relaxed text-gray-600">
					{opportunity.description}
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

			{/* mt-auto pins the chip to the bottom so cards in a row line their
			chips up regardless of how long the description above ran. */}
			<div className="mt-auto pt-4">
				<Chip tone="brand" size="sm">
					{formatParticipationType(opportunity.participationType, t)}
				</Chip>
			</div>
		</li>
	);
}
