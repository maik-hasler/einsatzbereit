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

export function formatDateTime(dt: string, locale: string = "en"): string {
	return new Date(dt).toLocaleString(locale === "de" ? "de-DE" : "en-GB", {
		dateStyle: "medium",
		timeStyle: "short",
	});
}

export function formatPostedAgo(dt: string, t: TFunction): string {
	const days = differenceInCalendarDays(new Date(), new Date(dt));
	return days <= 0
		? t("opportunities.postedToday")
		: t("opportunities.postedDaysAgo", { count: days });
}
