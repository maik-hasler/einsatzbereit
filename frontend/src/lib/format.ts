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

/** Remaining spots for a time slot/opportunity - null means unlimited capacity. */
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

/**
 * The earliest of an opportunity's time slots that hasn't ended yet - the
 * same "next" slot `VolunteerOpportunityReadRepository`'s browse listing
 * precomputes server-side as `nextTimeSlotStart`/`nextTimeSlotEnd` (ordered
 * by start, filtered to `EndDateTime >= now`). The opportunity detail
 * page's `VolunteerOpportunityDetails` contract doesn't carry that
 * precomputed pair - it hands over every slot instead, already ordered by
 * start ascending (`GetVolunteerOpportunityDetailsQueryHandler`) - so this
 * re-derives the same slot client-side instead of inventing a different
 * "next" than the one volunteers already see on the browse list for the
 * same opportunity (#2055).
 */
export function findNextTimeSlot(
	timeSlots: TimeSlotDetail[],
): TimeSlotDetail | undefined {
	const now = Date.now();
	return timeSlots.find(
		(ts) => new Date(ts.endDateTime as unknown as string).getTime() >= now,
	);
}

/**
 * The organizer-facing sign-up count - "3/10 sign-ups", "3 sign-ups (no
 * cap)" - for every state of the capacity contract (`lib/opportunityCapacity`).
 *
 * Lives here rather than in either caller because the organizer list and the
 * dashboard's upcoming widget had grown the same two-branch version of this
 * line independently, and both dropped it entirely in the third state (#1777).
 */
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

/**
 * Organizer-authored opportunity title/description carry a required German
 * variant and an optional English one (einsatzbereit#1946) - this picks
 * whichever matches the viewer's active UI language, falling back to German
 * when English wasn't provided (or is blank) rather than showing nothing.
 * Single source of truth so every card/list/detail surface that renders an
 * opportunity's title or description resolves the same way instead of each
 * re-deriving its own fallback.
 */
export function pickLocalizedText(
	textDe: string,
	textEn: string | null | undefined,
	lng: string,
): string;
export function pickLocalizedText(
	textDe: string | null | undefined,
	textEn: string | null | undefined,
	lng: string,
): string | undefined;
export function pickLocalizedText(
	textDe: string | null | undefined,
	textEn: string | null | undefined,
	lng: string,
): string | undefined {
	if (lng === "en" && textEn && textEn.trim().length > 0) return textEn;
	return textDe ?? undefined;
}

/** i18n's UI language ("de"/"en") -> the Intl/date-fns locale used for date
 * formatting. The app only ever runs with i18n.language "de" or "en" (see
 * i18n.ts's supportedLngs), so this always maps to a fixed regional variant
 * rather than trying to infer the viewer's actual region - single source of
 * truth for every call site that used to duplicate this ternary (#1267). */
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

/** `lng` is i18n.language ("de"/"en"), not an Intl locale - required rather
 * than defaulted so a call site that forgets to pass it is a compile error
 * instead of silently rendering en-GB (#1267). */
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

/**
 * The product-wide time-range string ("27.08.2026, 09:00-17:00") - collapses
 * the date to one side when `start`/`end` fall on the same calendar day
 * (in the viewer's local time zone) instead of repeating it on both ends,
 * since nearly every time slot is same-day and the doubled form was the
 * string most likely to wrap onto three lines in narrow cards (#2047). Falls
 * back to two full `formatDateTime` calls joined with " - " once the range
 * crosses a day boundary, so the date is never dropped where it's actually
 * needed. `lng` is i18n.language ("de"/"en"), not an Intl locale (see
 * formatDateTime).
 */
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

/** Date-only formatting (no time-of-day) - for deadlines like ValidUntil,
 * where the time component isn't meaningful to the viewer. `lng` is
 * i18n.language ("de"/"en"), not an Intl locale (see formatDateTime). */
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

/** Full spelled-out date with weekday (e.g. "Samstag, 1. August 2026") - for
 * screen-reader accessible names on calendar day cells (MiniCalendar's date
 * grid, CalendarWidget's month view) whose visible label is just a bare
 * number and needs a complete accessible name instead. `lng` is
 * i18n.language ("de"/"en"), not an Intl locale (see formatDateTime). */
export function formatFullDate(date: Date, lng: string): string {
	return getFullDateFormatter(lng).format(date);
}

export function formatPostedAgo(dt: string, t: TFunction): string {
	const days = differenceInCalendarDays(new Date(), new Date(dt));
	return days <= 0
		? t("opportunities.postedToday")
		: t("opportunities.postedDaysAgo", { count: days });
}

/** Window (in days) within which an organization is flagged as recently
 * created for admins, since it has no track record yet (#1947). */
export const NEW_ORGANIZATION_THRESHOLD_DAYS = 14;

export function isRecentlyCreatedOrganization(createdOn: string): boolean {
	const days = differenceInCalendarDays(new Date(), new Date(createdOn));
	return days <= NEW_ORGANIZATION_THRESHOLD_DAYS;
}

/** Window (in days) within which an application deadline is flagged with the
 * amber warning tone. Applying that tone to every deadline regardless of
 * distance - including ones months out - diluted the warning for one that is
 * actually close (#2088). */
export const DEADLINE_IMMINENT_THRESHOLD_DAYS = 7;

export function isDeadlineImminent(validUntil: string): boolean {
	const days = differenceInCalendarDays(new Date(validUntil), new Date());
	return days <= DEADLINE_IMMINENT_THRESHOLD_DAYS;
}
