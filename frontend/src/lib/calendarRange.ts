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

// Matches react-big-calendar's own default Agenda view length (see
// DEFAULT_LENGTH in its Agenda view) - the widget never overrides `length`,
// so requesting the same window keeps calendar-events data aligned with
// what the agenda view actually renders.
const AGENDA_LENGTH_DAYS = 30;

// Mirrors the date math react-big-calendar's own view components use
// internally to decide what's on screen (Month.range/Week.range/etc. via
// its default date-fns localizer) - CalendarWidget needs the exact same
// bounds *before* the calendar ever fires onRangeChange (which only runs on
// navigation, never on initial mount), so it's computed here directly from
// (date, view) instead of relying on that callback.
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
		case "work_week":
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
