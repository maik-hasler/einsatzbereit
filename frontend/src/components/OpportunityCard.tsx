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
import OrgAvatar from "./OrgAvatar";
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

	organizationId?: string;
	organizationName?: string;
	organizationLogoUrl?: string;
	tags?: string[];
	bannerImageUrl?: string;
}

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

	const isGermanFallback =
		title.lang !== i18n.language ||
		(description !== undefined && description.lang !== i18n.language);
	const hasOrganization = !!item.organizationId && !!item.organizationName;

	const showSignUpMechanismChip = item.participationType === "ScheduledSlots";

	return (
		<li className="group relative flex h-full flex-col rounded-card border border-gray-100 bg-white shadow-resting transition-shadow hover:shadow-raised">
			<Link
				to={`/volunteer-opportunities/${item.id}`}
				className="absolute inset-0 z-10 rounded-card"
				aria-label={title.text}
				lang={title.lang}
			/>
			<div className="flex h-full flex-col">
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
								<OrgAvatar
									name={item.organizationName ?? ""}
									logoUrl={item.organizationLogoUrl}
									size="lg"
									lazy
								/>
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
