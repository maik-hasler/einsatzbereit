import type { TFunction } from "i18next";
import { differenceInCalendarDays } from "date-fns";

export function formatOccurrence(occurrence: string, t: TFunction): string {
	return occurrence === "Recurring"
		? t("opportunities.recurring")
		: t("opportunities.oneTime");
}

export function formatParticipationType(type: string, t: TFunction): string {
	return type === "ScheduledSlots"
		? t("opportunities.waitlist")
		: t("opportunities.individualContact");
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

export function formatPostedAgo(dt: string, t: TFunction): string {
	const days = differenceInCalendarDays(new Date(), new Date(dt));
	return days <= 0
		? t("opportunities.postedToday")
		: t("opportunities.postedDaysAgo", { count: days });
}
