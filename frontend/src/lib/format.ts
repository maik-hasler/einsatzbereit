import type { TFunction } from "i18next";
import { differenceInCalendarDays } from "date-fns";

export function formatOccurrence(occurrence: string, t: TFunction): string {
	return occurrence === "Recurring"
		? t("opportunities.recurring")
		: t("opportunities.oneTime");
}

export function formatParticipationType(type: string, t: TFunction): string {
	return type === "Waitlist"
		? t("opportunities.waitlist")
		: t("opportunities.individualContact");
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
