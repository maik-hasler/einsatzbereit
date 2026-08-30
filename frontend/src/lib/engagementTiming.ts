import { addHours, subHours } from "date-fns";

// Mirrors TimeSlot.CheckInWindowBefore / CheckInWindowAfter on the backend
// (#2202). Used here only to decide which affordance a card shows - the
// backend re-checks the window on every check-in, so a stale or skewed client
// clock can label the window a little early/late but never widen the real
// guard.
export const CHECK_IN_WINDOW_BEFORE_HOURS = 1;
export const CHECK_IN_WINDOW_AFTER_HOURS = 2;

export type SlotDateTime = Date | string | null | undefined;

/**
 * `unscheduled` is an expression of interest: no slot, so no window - the
 * backend exempts those from the time check entirely.
 */
export type CheckInWindowState =
	"unscheduled" | "notYetOpen" | "open" | "closed";

export interface CheckInWindow {
	opensAt: Date;
	closesAt: Date;
}

export function getCheckInWindow(
	start: SlotDateTime,
	end: SlotDateTime,
): CheckInWindow | null {
	if (!start || !end) return null;
	return {
		opensAt: subHours(new Date(start), CHECK_IN_WINDOW_BEFORE_HOURS),
		closesAt: addHours(new Date(end), CHECK_IN_WINDOW_AFTER_HOURS),
	};
}

export function getCheckInWindowState(
	start: SlotDateTime,
	end: SlotDateTime,
	now: Date = new Date(),
): CheckInWindowState {
	const window = getCheckInWindow(start, end);
	if (!window) return "unscheduled";
	if (now < window.opensAt) return "notYetOpen";
	if (now > window.closesAt) return "closed";
	return "open";
}

/**
 * Whether the occurrence this engagement is for is over. False for an
 * expression of interest, which has no occurrence to be over.
 */
export function hasSlotEnded(
	end: SlotDateTime,
	now: Date = new Date(),
): boolean {
	if (!end) return false;
	return now >= new Date(end);
}
