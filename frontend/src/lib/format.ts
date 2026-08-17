import type { TFunction } from "i18next";
import { differenceInCalendarDays } from "date-fns";
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

const longDateFormatters = new Map<string, Intl.DateTimeFormat>();

function getLongDateFormatter(lng: string): Intl.DateTimeFormat {
	const resolvedLocale = resolveDateLocale(lng);
	let formatter = longDateFormatters.get(resolvedLocale);
	if (!formatter) {
		formatter = new Intl.DateTimeFormat(resolvedLocale, {
			day: "2-digit",
			month: "long",
			year: "numeric",
		});
		longDateFormatters.set(resolvedLocale, formatter);
	}
	return formatter;
}

/** Long-form date-only formatting (e.g. "25. Juli 2026") - for lower-frequency
 * "created on"/"sent on" timestamps where a spelled-out month reads better
 * than the compact numeric style `formatDate` uses. `lng` is i18n.language
 * ("de"/"en"), not an Intl locale (see formatDateTime). Centralized here so
 * call sites don't each re-derive their own `toLocaleDateString` options and
 * drift into a third date format alongside formatDate/formatDateTime (#986). */
export function formatDateLong(dt: string, lng: string): string {
	return getLongDateFormatter(lng).format(new Date(dt));
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
