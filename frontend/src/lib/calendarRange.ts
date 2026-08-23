import {
	startOfWeek,
	endOfWeek,
	startOfMonth,
	endOfMonth,
	startOfDay,
	endOfDay,
	addDays,
} from "date-fns";
import type { View } from "react-big-calendar";

const AGENDA_LENGTH_DAYS = 30;

export function visibleCalendarRange(
	date: Date,
	view: View,
): { from: Date; to: Date } {
	switch (view) {
		case "month":
			return {
				from: startOfWeek(startOfMonth(date)),
				to: endOfWeek(endOfMonth(date)),
			};
		case "week":
			return { from: startOfWeek(date), to: endOfWeek(date) };
		case "agenda":
			return {
				from: startOfDay(date),
				to: endOfDay(addDays(date, AGENDA_LENGTH_DAYS)),
			};
		case "day":
		default:
			return { from: startOfDay(date), to: endOfDay(date) };
	}
}
