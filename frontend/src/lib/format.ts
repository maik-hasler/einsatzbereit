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

const dateTimeFormatters = new Map<string, Intl.DateTimeFormat>();

function getDateTimeFormatter(locale: string): Intl.DateTimeFormat {
	const resolvedLocale = locale === "de" ? "de-DE" : "en-GB";
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

export function formatDateTime(dt: string, locale: string = "en"): string {
	return getDateTimeFormatter(locale).format(new Date(dt));
}

export function formatPostedAgo(dt: string, t: TFunction): string {
	const days = differenceInCalendarDays(new Date(), new Date(dt));
	return days <= 0
		? t("opportunities.postedToday")
		: t("opportunities.postedDaysAgo", { count: days });
}
