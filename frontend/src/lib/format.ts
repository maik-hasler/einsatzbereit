import type { TFunction } from "i18next";
import { differenceInCalendarDays, isSameDay } from "date-fns";
import type { TimeSlotDetail } from "../client/api-client";
import type { OpportunityCapacity } from "./opportunityCapacity";

export function formatOccurrence(occurrence: string, t: TFunction): string {
	return occurrence === "Recurring"
		? t("opportunities.recurring")
		: t("opportunities.oneTime");
}

export function formatParticipationType(type: string, t: TFunction): string {
	return type === "ScheduledSlots"
		? t("opportunities.waitlist")
		: t("opportunities.byInterest");
}

export function computeSpotsLeft(
	maxParticipants: number | null | undefined,
	bookedCount: number,
): number | null {
	return maxParticipants == null ? null : maxParticipants - bookedCount;
}

export function isSlotFull(
	maxParticipants: number | null | undefined,
	bookedCount: number,
): boolean {
	const spotsLeft = computeSpotsLeft(maxParticipants, bookedCount);
	return spotsLeft !== null && spotsLeft <= 0;
}

export function isTimeSlotEnded(
	ts: Pick<TimeSlotDetail, "endDateTime">,
): boolean {
	return new Date(ts.endDateTime as unknown as string).getTime() < Date.now();
}

export function findNextTimeSlot(
	timeSlots: TimeSlotDetail[],
): TimeSlotDetail | undefined {
	return timeSlots.find((ts) => !isTimeSlotEnded(ts));
}

export function formatSignUpCount(
	capacity: OpportunityCapacity,
	t: TFunction,
): string {
	switch (capacity.kind) {
		case "unlimited":
			return t("orgOpportunities.participantsUnlimited", {
				count: capacity.booked,
			});
		case "notApplicable":
			return capacity.reason === "interest"
				? t("orgOpportunities.participantsInterest", { count: capacity.booked })
				: t("orgOpportunities.participantsNoSlots", { count: capacity.booked });
		case "capped":
			return t("orgOpportunities.participants", {
				booked: capacity.booked,
				max: capacity.max,
			});
	}
}

export interface LocalizedText {
	text: string;
	lang: string;
}

export function pickLocalizedText(
	textDe: string,
	textEn: string | null | undefined,
	lng: string,
): LocalizedText;
export function pickLocalizedText(
	textDe: string | null | undefined,
	textEn: string | null | undefined,
	lng: string,
): LocalizedText | undefined;
export function pickLocalizedText(
	textDe: string | null | undefined,
	textEn: string | null | undefined,
	lng: string,
): LocalizedText | undefined {
	if (lng === "en" && textEn && textEn.trim().length > 0) {
		return { text: textEn, lang: "en" };
	}
	return textDe != null ? { text: textDe, lang: "de" } : undefined;
}

// Mirrors the backend's keyword search (title/description in both locales,
// plus the organization name - see ApplyPubliclyListedFilters), so it can
// tell whether a match the user can already see on the card explains a hit,
// or whether the hit only exists in a locale the card isn't displaying (#2242).
export function findCrossLocaleKeywordMatch(
	titleDe: string,
	titleEn: string | null | undefined,
	descriptionDe: string | null | undefined,
	descriptionEn: string | null | undefined,
	organizationName: string,
	keyword: string,
	displayedTitle: LocalizedText,
	displayedDescription: LocalizedText | undefined,
): LocalizedText | undefined {
	const needle = keyword.trim().toLowerCase();
	if (!needle) return undefined;

	const visibleTexts = [
		displayedTitle.text,
		displayedDescription?.text,
		organizationName,
	];
	if (visibleTexts.some((text) => text?.toLowerCase().includes(needle))) {
		return undefined;
	}

	const hiddenTexts: (LocalizedText | undefined)[] = [
		{ text: titleDe, lang: "de" },
		titleEn ? { text: titleEn, lang: "en" } : undefined,
		descriptionDe ? { text: descriptionDe, lang: "de" } : undefined,
		descriptionEn ? { text: descriptionEn, lang: "en" } : undefined,
	];

	return hiddenTexts.find(
		(candidate): candidate is LocalizedText =>
			candidate !== undefined && candidate.text.toLowerCase().includes(needle),
	);
}

export function resolveDateLocale(lng: string): string {
	return lng === "de" ? "de-DE" : "en-GB";
}

const dateTimeFormatters = new Map<string, Intl.DateTimeFormat>();

function getDateTimeFormatter(lng: string): Intl.DateTimeFormat {
	const resolvedLocale = resolveDateLocale(lng);
	let formatter = dateTimeFormatters.get(resolvedLocale);
	if (!formatter) {
		formatter = new Intl.DateTimeFormat(resolvedLocale, {
			dateStyle: "medium",
			timeStyle: "short",
		});
		dateTimeFormatters.set(resolvedLocale, formatter);
	}
	return formatter;
}

export function formatDateTime(dt: string, lng: string): string {
	return getDateTimeFormatter(lng).format(new Date(dt));
}

const timeFormatters = new Map<string, Intl.DateTimeFormat>();

function getTimeFormatter(lng: string): Intl.DateTimeFormat {
	const resolvedLocale = resolveDateLocale(lng);
	let formatter = timeFormatters.get(resolvedLocale);
	if (!formatter) {
		formatter = new Intl.DateTimeFormat(resolvedLocale, { timeStyle: "short" });
		timeFormatters.set(resolvedLocale, formatter);
	}
	return formatter;
}

export function formatDateTimeRange(
	startDt: string,
	endDt: string,
	lng: string,
): string {
	const start = new Date(startDt);
	const end = new Date(endDt);
	if (!isSameDay(start, end)) {
		return `${formatDateTime(startDt, lng)} - ${formatDateTime(endDt, lng)}`;
	}
	const datePart = getDateFormatter(lng).format(start);
	const startTime = getTimeFormatter(lng).format(start);
	const endTime = getTimeFormatter(lng).format(end);
	return `${datePart}, ${startTime}-${endTime}`;
}

const dateFormatters = new Map<string, Intl.DateTimeFormat>();

function getDateFormatter(lng: string): Intl.DateTimeFormat {
	const resolvedLocale = resolveDateLocale(lng);
	let formatter = dateFormatters.get(resolvedLocale);
	if (!formatter) {
		formatter = new Intl.DateTimeFormat(resolvedLocale, {
			dateStyle: "medium",
		});
		dateFormatters.set(resolvedLocale, formatter);
	}
	return formatter;
}

export function formatDate(dt: string, lng: string): string {
	return getDateFormatter(lng).format(new Date(dt));
}

const fullDateFormatters = new Map<string, Intl.DateTimeFormat>();

function getFullDateFormatter(lng: string): Intl.DateTimeFormat {
	const resolvedLocale = resolveDateLocale(lng);
	let formatter = fullDateFormatters.get(resolvedLocale);
	if (!formatter) {
		formatter = new Intl.DateTimeFormat(resolvedLocale, {
			weekday: "long",
			year: "numeric",
			month: "long",
			day: "numeric",
		});
		fullDateFormatters.set(resolvedLocale, formatter);
	}
	return formatter;
}

export function formatFullDate(date: Date, lng: string): string {
	return getFullDateFormatter(lng).format(date);
}

export function formatPostedAgo(dt: string, t: TFunction): string {
	const days = differenceInCalendarDays(new Date(), new Date(dt));
	return days <= 0
		? t("opportunities.postedToday")
		: t("opportunities.postedDaysAgo", { count: days });
}

export const NEW_ORGANIZATION_THRESHOLD_DAYS = 14;

export function isRecentlyCreatedOrganization(createdOn: string): boolean {
	const days = differenceInCalendarDays(new Date(), new Date(createdOn));
	return days <= NEW_ORGANIZATION_THRESHOLD_DAYS;
}

export const DEADLINE_IMMINENT_THRESHOLD_DAYS = 7;

export function isDeadlineImminent(validUntil: string): boolean {
	const days = differenceInCalendarDays(new Date(validUntil), new Date());
	return days <= DEADLINE_IMMINENT_THRESHOLD_DAYS;
}
